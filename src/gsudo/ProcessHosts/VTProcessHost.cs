﻿using gsudo.Helpers;
using gsudo.PseudoConsole;
using gsudo.Rpc;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace gsudo.ProcessHosts
{
    // PseudoConsole app host service. 
    // Speaks VT
    class VTProcessHost : IProcessHost
    {
        private Connection _connection;

        public async Task Start(Connection connection, ElevationRequest request)
        {
            int? exitCode;
            Task t1 = null, t2 = null;
            _connection = connection;
            try
            {
                string command = request.FileName + " " + request.Arguments;
                using (var inputPipe = new PseudoConsolePipe())
                using (var outputPipe = new PseudoConsolePipe())
                {
                    using (var pseudoConsole = PseudoConsole.PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide, (short)request.ConsoleWidth, (short)request.ConsoleHeight))
                    {
                        using (var process = ProcessFactory.StartPseudoConsole(command, PseudoConsole.PseudoConsole.PseudoConsoleThreadAttribute, pseudoConsole.Handle, request.StartFolder))
                        {
                            // copy all pseudoconsole output to stdout
                            t1 = Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));
                            // prompt for stdin input and send the result to the pseudoconsole
                            t2 = Task.Run(() => CopyInputToPipe(inputPipe.WriteSide));

                            Logger.Instance.Log($"Process ({process.ProcessInfo.dwProcessId}) started: {request.FileName} {request.Arguments}", LogLevel.Debug);
                            // free resources in case the console is ungracefully closed (e.g. by the 'x' in the window titlebar)
                            // var t3 = new StreamReader(pipe, Globals.Encoding).ConsumeOutput((s) => WriteToStdInput(s, process));

                            OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe));
                            WaitForExit(process).WaitOne();

                            if (connection.IsAlive)
                            {
                                await Task.Delay(10).ConfigureAwait(false);
                            }
                            exitCode = process.GetExitCode();
                        }
                    }
                }

                if (connection.IsAlive)
                {
                    await connection.ControlStream.WriteAsync($"{Constants.TOKEN_EXITCODE}{exitCode ?? 0}{Constants.TOKEN_EXITCODE}").ConfigureAwait(false);
                }

                await connection.FlushAndCloseAll().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ex.ToString(), LogLevel.Error);
                await connection.ControlStream.WriteAsync($"{Constants.TOKEN_ERROR}Server Error: {ex.ToString()}\r\n{Constants.TOKEN_ERROR}").ConfigureAwait(false);
                await connection.FlushAndCloseAll().ConfigureAwait(false);
                return;
            }
        }

        /// <summary>
        /// Reads terminal input and copies it to the PseudoConsole
        /// </summary>
        /// <param name="inputWriteSide">the "write" side of the pseudo console input pipe</param>
        private async Task CopyInputToPipe(SafeFileHandle inputWriteSide)
        {
            Stream debugStream = null;

            using (var inputWriteStream = new FileStream(inputWriteSide, FileAccess.Write))
            using (var writer = new StreamWriter(inputWriteStream))
            {
                writer.AutoFlush = true;
                while (true)
                {
                    byte[] buffer = new byte[256];
                    int cch;

                    while ((cch = await _connection.DataStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        var s = GlobalSettings.Encoding.GetString(buffer, 0, cch);
                        writer.Write(s);

                        if (GlobalSettings.Debug)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(s);
                            Console.ResetColor();
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Reads PseudoConsole output and copies it to the terminal's standard out.
        /// </summary>
        /// <param name="outputReadSide">the "read" side of the pseudo console output pipe</param>
//        private void CopyPipeToOutput(SafeFileHandle outputReadSide)
        private async Task CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            Stream debugStream = null;

            if (GlobalSettings.Debug)
            {
                debugStream = new FileStream("VTProcessHost.debug.txt", FileMode.Create, FileAccess.Write);
            }

            using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
            {
                byte[] buffer = new byte[10240];
                int cch;

                while ((cch = await pseudoConsoleOutput.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    var s = GlobalSettings.Encoding.GetString(buffer, 0, cch);
                    await _connection.DataStream.WriteAsync(s).ConfigureAwait(false);

                    debugStream?.Write(GlobalSettings.Encoding.GetBytes(s), 0, s.Length);
                    debugStream?.Flush();

                    //Console.Write(s);

                }
            }
            debugStream?.Close();
        }

        /// <summary>
        /// Get an AutoResetEvent that signals when the process exits
        /// </summary>
        private static AutoResetEvent WaitForExit(Process process) =>
            new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };

        /// <summary>
        /// Set a callback for when the terminal is closed (e.g. via the "X" window decoration button).
        /// Intended for resource cleanup logic.
        /// </summary>
        private static void OnClose(Action handler)
        {
            Native.ConsoleApi.SetConsoleCtrlHandler(eventType =>
            {
                if (eventType == Native.ConsoleApi.CtrlTypes.CTRL_CLOSE_EVENT)
                {
                    handler();
                }
                return false;
            }, true);
        }

        private void DisposeResources(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }

        private bool ShouldWait(StreamReader streamReader)
        {
            try
            {
                return !streamReader.EndOfStream;
            }
            catch
            {
                return false;
            }
        }

        static readonly string[] TOKENS = new string[] { "\0", Constants.TOKEN_KEY_CTRLBREAK, Constants.TOKEN_KEY_CTRLC};
      
    }
}