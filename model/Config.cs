using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using watchCode.helpers;

namespace watchCode.model
{
    public class Config
    {
        /// <summary>
        /// the absolute path to the source files (OR . for current working dir)
        /// </summary>
        public string SourceFilesDirAbsolutePath { get; set; }

        /// <summary>
        /// the absolute path to the doc files (OR . for current working dir)
        /// </summary>
        public string DocFilesDirAbsolutePath { get; set; }


        public List<string> DocDirsToIgnore { get; set; }
        public List<string> DocFilesToIgnore { get; set; }

        public List<string> SourceDirsToIgnore { get; set; }
        public List<string> SourceFilesToIgnore { get; set; }


        /// <summary>
        /// true: check dirs recursively
        /// </summary>
        public bool? RecursiveCheckDocDirs { get; set; }

//        /// <summary>
//        /// true: check <see cref="SourceDirs"/> recursively
//        /// </summary>
//        public bool? RecursiveCheckSourceDirs { get; set; }


//        /// <summary>
//        /// true: reduce/compress watch expressions e.g. if one watch expression (line range) is included
//        /// in another, this is faster for evaulating but takes some time for finding the "duplicates"
//        /// false: use all found watch expressions 
//        /// </summary>
//        [Obsolete("not working")]
//        public bool? ReduceWatchExpressions { get; set; }


        /// <summary>
        /// see <see cref="HashHelper.SetHashAlgorithm"/> for supported algorithms
        /// null or whitespaces for default
        /// </summary>
        public string HashAlgorithmToUse { get; set; }

        /// <summary>
        /// the dir name to store stuff (relative to <see cref="DocFilesDirAbsolutePath"/>)
        /// </summary>
        public string WatchCodeDirName { get; set; }

        /// <summary>
        /// relative to <see cref="WatchCodeDirName"/>
        /// </summary>
        public string SnapshotDirName { get; set; }

        /// <summary>
        /// true: only create a dump file containg all
        /// watch expression discovered inside the dirs/files
        /// </summary>
        public bool? CreateWatchExpressionsDumpFile { get; set; }

        /// <summary>
        /// will be created inside <see cref="WatchCodeDirName"/>
        /// </summary>
        public string DumpWatchExpressionsFileName { get; set; }


        public bool? IgnoreHiddenFiles { get; set; }


        //--- not accessible via command line

        /// <summary>
        /// the keywords to initiate a watch expression
        /// 
        /// THIS is only for serialization use for real usage <see cref="DynamicConfig.InitWatchExpressionKeywords"/>
        /// </summary>
        public List<string> InitWatchExpressionKeywords { get; set; }

        /// <summary>
        /// a dictionary with extensions and comments syntax to know which documention files we need to consider
        /// 
        /// THIS is only for serialization use for real usage <see cref="DynamicConfig.KnownFileExtensionsWithoutExtension"/>
        /// </summary>
        public Dictionary<string, List<CommentPattern>> KnownFileExtensionsWithoutExtension { get; set; }


        /// <summary>
        /// checks if every prop has a != null value
        /// 
        /// also if some args are set that could not be used with some config options...
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static bool Validate(Config config, CmdArgs cmdArgs)
        {
            var props = config
                .GetType()
                .GetProperties();

            foreach (var prop in props)
            {
                if (prop.GetValue(config) == null)
                {
                    Logger.Error($"config property: {prop.Name} was null but needs a value");
                    return false;
                }
            }


            foreach (var filePath in config.DocFilesToIgnore)
            {
                var absolutePath = DynamicConfig.GetAbsoluteDocFilePath(filePath);
                if (!File.Exists(absolutePath))
                    Logger.Warn($"ignoring doc file on doc ignore list: {absolutePath} because it does not exist");
            }


            foreach (var filePath in config.DocDirsToIgnore)
            {
                var absolutePath = DynamicConfig.GetAbsoluteDocFilePath(filePath);
                if (!Directory.Exists(absolutePath))
                    Logger.Warn($"ignoring directory on doc ignore list: {absolutePath} because it does not exist");
            }


//            foreach (var filePath in config.SourceFiles)
//            {
//                var absolutePath = DynamicConfig.GetAbsoluteSourceFilePath(filePath);
//                if (!File.Exists(absolutePath))
//                    Logger.Warn($"ignoring source file to check: {absolutePath} because it does not exist");
//            }

            foreach (var filePath in config.SourceFilesToIgnore)
            {
                var absolutePath = DynamicConfig.GetAbsoluteSourceFilePath(filePath);
                if (!File.Exists(absolutePath))
                    Logger.Warn(
                        $"ignoring source file on source ignore list: {absolutePath} because it does not exist");
            }

//            foreach (var filePath in config.SourceDirs)
//            {
//                var absolutePath = DynamicConfig.GetAbsoluteSourceFilePath(filePath);
//                if (!Directory.Exists(absolutePath))
//                    Logger.Warn($"ignoring directory to check: {absolutePath} because it does not exist");
//            }

            foreach (var filePath in config.SourceDirsToIgnore)
            {
                var absolutePath = DynamicConfig.GetAbsoluteSourceFilePath(filePath);
                if (!Directory.Exists(absolutePath))
                    Logger.Warn($"ignoring directory on source ignore list: {absolutePath} because it does not exist");
            }


            //config.KnownFileExtensionsWithoutExtension was only temp read from the config...
            if (DynamicConfig.KnownFileExtensionsWithoutExtension.Count == 0)
            {
                Logger.Error($"the known extensions map was empty... thus  no files will be found");
                return false;
            }

            foreach (var pair in DynamicConfig.KnownFileExtensionsWithoutExtension)
            {
                if (pair.Value.Count == 0)
                {
                    Logger.Error($"a known extensions map entry was empty, key: {pair.Key}");
                    return false;
                }
            }

            //config.InitWatchExpressionKeywords was only temp read from the config...
            if (DynamicConfig.InitWatchExpressionKeywords.Count == 0)
            {
                Logger.Error($"the init watch expression keywords map was empty... thus no files will be found");
                return false;
            }


            if (cmdArgs.MainAction == MainAction.UpdateExpression &&
                (string.IsNullOrWhiteSpace(cmdArgs.UpdateDocsOldWatchExpression) ||
                 string.IsNullOrWhiteSpace(cmdArgs.UpdateDocsNewWatchExpression)))
            {
                Logger.Error($"UpdateExpression some expr empty");
                return false;
            }

            return true;
        }


        public static Config DefaultConfig = new Config()
        {
            DocFilesDirAbsolutePath = ".",
            SourceFilesDirAbsolutePath = ".",

            DocFilesToIgnore = new List<string>(),
            DocDirsToIgnore = new List<string>(),
            SourceDirsToIgnore = new List<string>(),
            SourceFilesToIgnore = new List<string>(),

            WatchCodeDirName = "__watchCode__",
            SnapshotDirName = "__snapshots__",
            DumpWatchExpressionsFileName = "watchExpressions.json",
            CreateWatchExpressionsDumpFile = false,
            RecursiveCheckDocDirs = true,
//            RecursiveCheckSourceDirs = true,
            IgnoreHiddenFiles = true,
            HashAlgorithmToUse = HashHelper.DefaultHashAlgorithmName,
            InitWatchExpressionKeywords = new List<string>(),
            KnownFileExtensionsWithoutExtension = new Dictionary<string, List<CommentPattern>>()
        };
    }
}
