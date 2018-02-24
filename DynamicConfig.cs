using System.Collections.Generic;
using System.IO;
using watchCode.model;

namespace watchCode
{
    public static class DynamicConfig
    {
        public static string DocFilesDirAbsolutePath = null;

        public static string SourceFilesDirAbsolutePath = null;

        public static Dictionary<string, List<CommentPattern>> KnownFileExtensionsWithoutExtension;

        public static List<string> InitWatchExpressionKeywords;

        static DynamicConfig()
        {
            KnownFileExtensionsWithoutExtension = new Dictionary<string, List<CommentPattern>>();

            KnownFileExtensionsWithoutExtension.Add("md", new List<CommentPattern>()
            {
                new CommentPattern()
                {
                    StartCommentPart = "<!--",
                    EndCommentPart = "-->",
                }
            });

            InitWatchExpressionKeywords = new List<string>()
            {
                "@watch"
            };
        }

        public static string GetAbsoluteWatchCodeDirPath(Config config)
        {
            return Path.Combine(DocFilesDirAbsolutePath, config.WatchCodeDirName);
        }
        
        
        public static string GetAbsoluteSnapShotsDirPath(Config config)
        {
            return Path.Combine(GetAbsoluteWatchCodeDirPath(config), config.SnapshotDirName);
        }

        public static string GetAbsoluteDocFilePath(string watchExpressionFilePath)
        {
            return Path.Combine(DocFilesDirAbsolutePath, watchExpressionFilePath);
        }
        public static string GetAbsoluteSourceFilePath(string watchExpressionFilePath)
        {
            return Path.Combine(SourceFilesDirAbsolutePath, watchExpressionFilePath);
        }
    }
}
