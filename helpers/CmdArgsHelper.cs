using watchCode.model;

namespace watchCode.helpers
{
    public static class CmdArgsHelper
    {
        private const string ParameterStart = "-";

        private static bool insideDirs = false;
        private static bool insideFiles = false;
        private static bool insideRecursiveCheckDirs = false;
        private static bool insideCreateWatchExpressionsDumpFile = false;
        private static bool insideWatchDir = false;
        private static bool insideSnapshotDirName = false;
        private static bool insideDumpWatchExpressionsFileName = false;
        private static bool insideRootDir = false;
        private static bool insideNoReduce = false;
        private static bool insideNoCombineSnapshots = false;
        private static bool insideNoCompressLines = false;
        private static bool insideHashAlgorithmToUse = false;

        public static CmdArgs ParseArgs(string[] args)
        {
            var cmdArgs = new CmdArgs();


            foreach (var arg in args)
            {
                switch (arg)
                {
                    case ParameterStart + "dir":
                    case ParameterStart + "dirs":
                        ResetInsideArgs();
                        insideDirs = true;
                        continue;
                        break;

                    case ParameterStart + "file":
                    case ParameterStart + "files":
                        ResetInsideArgs();
                        insideFiles = true;
                        continue;
                        break;

                    case ParameterStart + "notRecursive":
                        ResetInsideArgs();
                        insideRecursiveCheckDirs = true;
                        continue;
                        break;

                    case ParameterStart + "dump":
                        ResetInsideArgs();
                        insideCreateWatchExpressionsDumpFile = true;
                        continue;
                        break;

                    case ParameterStart + "watchDir":
                        ResetInsideArgs();
                        insideWatchDir = true;
                        continue;
                        break;

                    case ParameterStart + "snapshotDirName":
                        ResetInsideArgs();
                        insideSnapshotDirName = true;
                        continue;
                        break;

                    case ParameterStart + "dumpName":
                        ResetInsideArgs();
                        insideDumpWatchExpressionsFileName = true;
                        continue;
                        break;

                    case ParameterStart + "rootDir":
                        ResetInsideArgs();
                        insideRootDir = true;
                        continue;
                        break;

                    case ParameterStart + "noReduce":
                        ResetInsideArgs();
                        insideNoReduce = true;
                        continue;
                        break;

                    case ParameterStart + "noCombineSnapshots":
                        ResetInsideArgs();
                        insideNoCombineSnapshots = true;
                        continue;
                        break;
                        
                    case ParameterStart + "plainLines":
                        ResetInsideArgs();
                        insideNoCompressLines = true;
                        continue;
                        break;
                        
                    case ParameterStart + "hashAlgo":
                        ResetInsideArgs();
                        insideHashAlgorithmToUse = true;
                        continue;
                        break;
                }

                if (insideDirs) cmdArgs.Dirs.Add(arg);
                if (insideFiles) cmdArgs.Files.Add(arg);

                if (insideRecursiveCheckDirs) cmdArgs.RecursiveCheckDirs = false;
                if (insideCreateWatchExpressionsDumpFile) cmdArgs.CreateWatchExpressionsDumpFile = true;
                if (insideWatchDir) cmdArgs.WatchCodeDirName = arg;
                if (insideSnapshotDirName) cmdArgs.SnapshotDirName = arg;

                if (insideDumpWatchExpressionsFileName) cmdArgs.DumpWatchExpressionsFileName = arg;
                if (insideRootDir) cmdArgs.RootDir = arg;
                if (insideNoReduce) cmdArgs.ReduceWatchExpressions = false;
                if (insideNoCombineSnapshots) cmdArgs.CombineSnapshotFiles = false;
                if (insideNoCompressLines) cmdArgs.CompressLines = false;
                if (insideHashAlgorithmToUse) cmdArgs.HashAlgorithmToUse = arg;
            }

            return cmdArgs;
        }


        private static void ResetInsideArgs()
        {
            insideDirs = false;
            insideFiles = false;
            insideRecursiveCheckDirs = false;
            insideCreateWatchExpressionsDumpFile = false;
            insideWatchDir = false;
            insideSnapshotDirName = false;
            insideDumpWatchExpressionsFileName = false;
            insideRootDir = false;
            insideNoReduce = false;
            insideNoCombineSnapshots = false;
            insideNoCompressLines = false;
            insideHashAlgorithmToUse = false;
        }
    }
}