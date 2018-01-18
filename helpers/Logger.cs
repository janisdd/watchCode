using System;

namespace watchCode.helpers
{
    public static class Logger
    {

        public static void Info(string text)
        {
            Console.WriteLine(text);
        }
        
        public static void Log(string text)
        {
            Console.WriteLine(text);
        }

        public static void Warn(string text)
        {
            Console.WriteLine(text);
        }

        public static void Error(string text)
        {
            Console.WriteLine(text);
        }
        
    }
}