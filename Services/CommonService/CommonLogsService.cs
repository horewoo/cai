﻿using CharacterEngineDiscord.Models.Common;

namespace CharacterEngineDiscord.Services
{
    internal static partial class CommonService
    {
        internal static void Log(object? text)
        {
            Console.Write($"{text + (text is string ? "" : "\n")}");
        }

        internal static void LogGreen(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Log(text);
            Console.ResetColor();
        }
        internal static void LogRed(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log(text);
            Console.ResetColor();
        }

        internal static void LogYellow(object? text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Log(text);
            Console.ResetColor();
        }

        internal static void LogException(object?[]? text)
        {
            if (text is null) return;

            int ww;
            try
            {
                ww = Console.WindowWidth;
            }
            catch
            {
                ww = 320;
            }

            LogRed(new string('~', ww - 1) + "\n");
            LogRed($"{string.Join('\n', text)}\n");
            LogRed(new string('~', ww - 1) + "\n");

            if (!ConfigFile.LogFileEnabled.Value.ToBool()) return;

            try {
                var sw = File.AppendText($"{EXE_DIR}{SC}logs.txt");
                sw.WriteLine($"{new string('~', ww)}\n{string.Join('\n', text)}\n");
                sw.Close();
            }
            catch (Exception e) { LogRed(e); }
        }
    }
}
