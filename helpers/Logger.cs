using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using watchCode.model;

namespace watchCode.helpers
{
    public static class Logger
    {
        public static string InfoPrefix = "[Info]";
        public static string WarningPrefix = "[Warn]";
        public static string ErrorPrefix = "[Error]";

        public static ConsoleColor InfoColor = Console.ForegroundColor; //use default
        public static ConsoleColor WarningColor = ConsoleColor.DarkYellow;
        public static ConsoleColor ErrorColor = ConsoleColor.DarkRed;

        private static Queue<string> logQueue = new Queue<string>();

        /// <summary>
        /// the log level to use
        /// the log will be printed to stdout if specified
        /// however errors will always be printed to stderr
        /// </summary>
        public static LogLevel LogLevel = LogLevel.Info;

        /// <summary>
        /// overwrites the last one if we have too much entries,
        ///  leq 0 to keep all
        /// </summary>
        public static int MaxLogEntriesToKeep = 10000;

        public static bool OutputLogToConsole = false;

        public static string LogFileName = "log.txt";


        /// <summary>
        /// logs an info (for verbose mode)
        /// </summary>
        /// <param name="text"></param>
        public static void Info(string text)
        {
            if (LogLevel == LogLevel.Info)
            {
                var message = $"{InfoPrefix} {text}";
                EnsureMaxEntiresAndAdd(message, LogLevel.Info);
            }
        }

        /// <summary>
        /// logs a warning
        /// </summary>
        /// <param name="text"></param>
        public static void Warn(string text)
        {
            if (LogLevel == LogLevel.Info || LogLevel == LogLevel.Warn)
            {
                var message = $"{WarningPrefix} {text}";
                EnsureMaxEntiresAndAdd(message, LogLevel.Warn);
            }
        }

        /// <summary>
        /// logs an error
        /// </summary>
        /// <param name="text"></param>
        public static void Error(string text)
        {
            string message = $"{ErrorPrefix} {text}";
            if (LogLevel == LogLevel.Info || LogLevel == LogLevel.Warn || LogLevel == LogLevel.Error)
            {
                EnsureMaxEntiresAndAdd(message, LogLevel.Error);
            }
        }

        /// <summary>
        /// ensures that the <see cref="MaxLogEntriesToKeep"/> is enforced and adds the message
        /// </summary>
        /// <param name="message">the message to add</param>
        /// <param name="severityLevel">the severity level used to color the output</param>
        private static void EnsureMaxEntiresAndAdd(string message, LogLevel severityLevel)
        {
            if (logQueue.Count >= MaxLogEntriesToKeep)
            {
                logQueue.Dequeue();
            }


            if (OutputLogToConsole)
            {
                switch (severityLevel)
                {
                    case LogLevel.None:
                        break;
                    case LogLevel.Info:
                        Console.ForegroundColor = InfoColor;
                        break;
                    case LogLevel.Warn:
                        Console.ForegroundColor = WarningColor;
                        break;
                    case LogLevel.Error:
                        Console.ForegroundColor = ErrorColor;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(severityLevel), severityLevel, null);
                }

                if (LogLevel == LogLevel.Error)
                {
                    Console.Error.WriteLine(message);
                }
                else
                {
                    Console.WriteLine(message);
                }

                Console.ResetColor();
            }

            logQueue.Enqueue(message);
        }

        /// <summary>
        /// returns the log as string (new line separated)
        /// </summary>
        /// <returns></returns>
        public static string GetLogAsString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var message in logQueue)
            {
                builder.AppendLine(message);
            }
            return builder.ToString();
        }

        public static void WriteLog(Config config)
        {
            string absoluteFilePath = Path.Combine(DynamicConfig.GetAbsoluteWatchCodeDirPath(config.WatchCodeDirName),
                LogFileName);

            try
            {
                var fileInfo = new FileInfo(absoluteFilePath);
                File.WriteAllText(fileInfo.FullName, GetLogAsString());
            }
            catch (Exception e)
            {
                Error($"could not create log file at: {absoluteFilePath}, error: {e.Message}");
                return;
            }
        }
    }
}
