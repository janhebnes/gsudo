﻿using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class HelpCommand : ICommand
    {
        public Task<int> Execute()
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        internal static void ShowVersion()
        {
            var asm = Assembly.GetExecutingAssembly().GetName();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{asm.Name} v{Regex.Replace(asm.Version.ToString(), "^(.*?)(\\.0)*$", "$1")}");
            Console.ResetColor();
            Console.WriteLine("Copyright(c) 2019 Gerardo Grignoli");
        }

        internal static void ShowHelp()
        {
            ShowVersion();
            Console.WriteLine("\nUsage:\n------");
            Console.WriteLine("gsudo \r\n\tRuns a shell with elevated permissions\r\n");
            Console.WriteLine("gsudo [options] {command} [arguments] \r\n\tRuns {command} with elevated permissions\r\n");
            Console.WriteLine("gsudo config\r\n\tShow current-user settings.\r\n");
            Console.WriteLine("gsudo config {key} [value | --reset] \r\n\tRead or write a user setting\r\n");
            Console.WriteLine("gsudo [-h | --help]    \t Shows this help");
            Console.WriteLine("gsudo [-v | --version] \t Shows gsudo version");
            Console.WriteLine();
            Console.WriteLine("Valid options:");
            Console.WriteLine(" -n | --new        Starts the command in a new console (and returns immediately).");
            Console.WriteLine(" -w | --wait       Force wait for the command to end.");
            Console.WriteLine(" -s | --system     Run As Local System account (\"NT AUTHORITY\\SYSTEM\").");
            Console.WriteLine(" --copyev          Copy environment variables to the elevated process before executing.");
            Console.WriteLine(" --copyns          Connect current network drives to the elevated user. Warning! This is verbose, affects the elevated user system-wide, and can prompt for credentials interactively.");
            Console.WriteLine(" --raw             Force use of a reduced terminal.");
            Console.WriteLine(" --vt              Force use of full VT100 terminal emulator (experimental).");
            Console.WriteLine(" --loglevel {val}  Only show logs where level is at least the value specified. Valid values are: All, Debug, Info, Warning, Error, None");
            Console.WriteLine(" --debug           Enable debug mode. (makes gsudo service window visible)");

            return;
        }
    }
}
