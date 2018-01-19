using System.Collections.Generic;
using watchCode.helpers;

namespace watchCode.model
{
    /// <summary>
    /// all props are possible params prepended with - and camelCase
    /// order does not matter
    /// </summary>
    public class CmdArgs
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
        public bool RecursiveCheckDirs { get; set; }

        /// <summary>
        /// true: reduce/compress watch expressions e.g. if one watch expression (line range) is included
        /// in another, this is faster for evaulating but takes some time for finding the "duplicates"
        /// false: use all found watch expressions 
        /// </summary>
        public bool ReduceWatchExpressions { get; set; }


        /// <summary>
        /// true: combines snapshots of the same file (code file) into one snapshot file
        /// false: not
        /// </summary>
        public bool CombineSnapshotFiles { get; set; }
        
        
        /// <summary>
        /// true: only create a dump file containg all
        /// watch expression discovered inside the dirs/files
        /// </summary>
        public bool CreateWatchExpressionsDumpFile { get; set; }

        /// <summary>
        /// will be created inside <see cref="WatchCodeDirName"/>
        /// </summary>
        public string DumpWatchExpressionsFileName { get; set; }

        /// <summary>
        /// true: use the real lines to compare snapshots
        /// false: use (md5) hash of the lines
        /// </summary>
        public bool CompressLines { get; set; }
        
        /// <summary>
        /// true: creates all snapshots (if not already exist)
        /// so especially not overwriting anything
        /// </summary>
        public bool Init { get; set; }
        
        
        /// <summary>
        /// true: updates the all snapshots
        /// if not exist then it will be created
        /// </summary>
        public bool Update { get; set; }
        
        /// <summary>
        /// true: compare the stored snapshots with the expression results
        /// </summary>
        public bool Compare { get; set; }


        public bool SilentCompare { get; set; }

        
        public string WatchCodeDirName { get; set; }

        /// <summary>
        /// see <see cref="HashHelper.SetHashAlgorithm"/> for supported algorithms
        /// null or whitespaces for default
        /// </summary>
        public string HashAlgorithmToUse { get; set; }
        
        /// <summary>
        /// relative to <see cref="WatchCodeDirName"/>
        /// </summary>
        public string SnapshotDirName { get; set; }

        public CmdArgs()
        {
            Dirs = new List<string>();
            Files = new List<string>();

            WatchCodeDirName = "__watchCode__";
            SnapshotDirName = "__snapshots__";
            DumpWatchExpressionsFileName = "watchExpressions.json";
            CreateWatchExpressionsDumpFile = true;
            RecursiveCheckDirs = true;
            ReduceWatchExpressions = true;
            CombineSnapshotFiles = true;
            CompressLines = true;
            RootDir = "";
            HashAlgorithmToUse = "";

        }
        
    }
}