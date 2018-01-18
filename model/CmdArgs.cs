using System.Collections.Generic;
using System.IO;

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
        /// true: only create a dump file containg all
        /// watch expression discovered inside the dirs/files
        /// </summary>
        public bool CreateWatchExpressionsDumpFile { get; set; }

        /// <summary>
        /// will be created inside <see cref="WatchCodeDir"/>
        /// </summary>
        public string DumpWatchExpressionsFileName { get; set; }

        
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

        
        public string WatchCodeDir { get; set; }
        
        /// <summary>
        /// relative to <see cref="WatchCodeDir"/>
        /// </summary>
        public string SnapshotDirName { get; set; }

        public CmdArgs()
        {
            Dirs = new List<string>();
            Files = new List<string>();

            WatchCodeDir = "__watchCode__";
            SnapshotDirName = "__snapshots__";
            DumpWatchExpressionsFileName = "watchExpressions.json";
            CreateWatchExpressionsDumpFile = true;
            RootDir = "";

        }
        
    }
}