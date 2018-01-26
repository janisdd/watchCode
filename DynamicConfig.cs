using System.Collections.Generic;
using System.IO;
using watchCode.model;

namespace watchCode
{
    public static class DynamicConfig
    {
        public static string AbsoluteRootDirPath = null;

        public static Dictionary<string, List<(string start, string end)>> KnownFileExtensionsWithoutExtension;

        public static List<string> InitWatchExpressionKeywords;

        static DynamicConfig()
        {
            KnownFileExtensionsWithoutExtension = new Dictionary<string, List<(string start, string end)>>();

            KnownFileExtensionsWithoutExtension.Add("md", new List<(string start, string end)>()
            {
                ("<!--", "-->")
            });

            InitWatchExpressionKeywords = new List<string>()
            {
                "@watch"
            };
        }

        public static string GetAbsoluteWatchCodeDirPath(Config config)
        {
            return Path.Combine(AbsoluteRootDirPath, config.WatchCodeDirName);
        }

        public static string GetAbsoluteSnapShotDirPath(Config config)
        {
            return Path.Combine(GetAbsoluteWatchCodeDirPath(config), config.SnapshotDirName);
        }

        public static string GetAbsoluteFilePath(string watchExpressionFilePath)
        {
            return Path.Combine(AbsoluteRootDirPath, watchExpressionFilePath);
        }
    }
}
