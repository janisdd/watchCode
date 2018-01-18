using System.Collections.Generic;
using System.IO;
using watchCode.model;

namespace watchCode
{
    public static class DynamicConfig
    {
        public static string AbsoluteRootDirPath = null;

        public static Dictionary<string, List<(string start, string end)>> KnownFileExtensionsWithoutExtension;

        public static List<string> initWatchExpressionKeywords;


        static DynamicConfig()
        {
            KnownFileExtensionsWithoutExtension = new Dictionary<string, List<(string start, string end)>>();

            KnownFileExtensionsWithoutExtension.Add("md", new List<(string start, string end)>()
            {
                ("<!--", "-->")
            });

            initWatchExpressionKeywords = new List<string>()
            {
                "@watch"
            };
        }

        public static string GetAbsoluteWatchCodeDirPath(CmdArgs cmdArgs)
        {
            return Path.Combine(AbsoluteRootDirPath, cmdArgs.WatchCodeDir);
        }

        public static string GetAbsoluteSnapShotDirPath(CmdArgs cmdArgs)
        {
            return Path.Combine(GetAbsoluteWatchCodeDirPath(cmdArgs), cmdArgs.SnapshotDirName);
        }

        public static string GetAbsoluteFileToWatchPath(string watchExpressionFilePath)
        {
            return Path.Combine(AbsoluteRootDirPath, watchExpressionFilePath);
        }
    }
}