using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using watchCode.helpers;

namespace watchCode.model
{
    public class Config
    {
        /// <summary>
        /// the root dir to use
        /// if not specified the current working dir is used
        /// </summary>
        public string RootDir { get; set; }

        public List<string> Dirs { get; set; }

        public List<string> Files { get; set; }

        /// <summary>
        /// true: check <see cref="Dirs"/> recursively
        /// </summary>
        public bool? RecursiveCheckDirs { get; set; }


        /// <summary>
        /// true: reduce/compress watch expressions e.g. if one watch expression (line range) is included
        /// in another, this is faster for evaulating but takes some time for finding the "duplicates"
        /// false: use all found watch expressions 
        /// </summary>
        [Obsolete("not working")]
        public bool? ReduceWatchExpressions { get; set; }


        /// <summary>
        /// true: combines snapshots of the same file (code file) into one snapshot file
        /// false: not
        /// </summary>
        public bool? CombineSnapshotFiles { get; set; }

        /// <summary>
        /// true: use the real lines to compare snapshots
        /// false: use (md5) hash of the lines
        /// </summary>
        public bool? CompressLines { get; set; }

        /// <summary>
        /// see <see cref="HashHelper.SetHashAlgorithm"/> for supported algorithms
        /// null or whitespaces for default
        /// </summary>
        public string HashAlgorithmToUse { get; set; }

        /// <summary>
        /// relative to <see cref="WatchCodeDirName"/>
        /// </summary>
        public string SnapshotDirName { get; set; }

        /// <summary>
        /// the dir to store stuff
        /// </summary>
        public string WatchCodeDirName { get; set; }

        /// <summary>
        /// true: only create a dump file containg all
        /// watch expression discovered inside the dirs/files
        /// </summary>
        public bool? CreateWatchExpressionsDumpFile { get; set; }

        /// <summary>
        /// will be created inside <see cref="WatchCodeDirName"/>
        /// </summary>
        public string DumpWatchExpressionsFileName { get; set; }

        
        public bool AlsoUseReverseLines { get; set; }

        //--- not accessible via command line
        
        /// <summary>
        /// the keywords to initiate a watch expression
        /// </summary>
        public List<string> InitWatchExpressionKeywords;

        /// <summary>
        /// a dictionary with extensions and comments syntax to know which documention files we need to consider
        /// </summary>
        public Dictionary<string, List<(string start, string end)>> KnownFileExtensionsWithoutExtension;

        
        

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
            return true;
        }

        public static Config DefaultConfig = new Config()
        {
            Dirs = new List<string>()
            {
                "." //current dir
            },
            Files = new List<string>(),

            WatchCodeDirName = "__watchCode__",
            SnapshotDirName = "__snapshots__",
            DumpWatchExpressionsFileName = "watchExpressions.json",
            CreateWatchExpressionsDumpFile = true,
            RecursiveCheckDirs = true,
            ReduceWatchExpressions = false,
            CombineSnapshotFiles = true,
            CompressLines = true,
            RootDir = "",
            HashAlgorithmToUse = HashHelper.DefaultHashAlgorithmName,
            AlsoUseReverseLines = false,

            //null beacuse we want to know if the user put an empty array in config
            InitWatchExpressionKeywords = null,
            KnownFileExtensionsWithoutExtension = null,
        };
    }
}