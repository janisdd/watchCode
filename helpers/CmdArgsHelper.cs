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
        private static bool insideHashAlgorithmToUse = false;
        private static bool insideConfigFileName = false;
        private static bool insideFilesToIgnore = false;
        private static bool insideDirsToIgnore = false;
        private static bool insideNotUseInMemoryStringBuilderFileForUpdateingDocs = false;
        private static bool insideWriteLog = false;
        private static bool insideShowLog = false;
        private static bool insideLogLevel = false;

        public static (CmdArgs, Config) ParseArgs(string[] args)
        {
            var cmdArgs = new CmdArgs();
            var config = Config.DefaultConfig;

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case ParameterStart + "rootDir":
                        ResetInsideArgs();
                        insideRootDir = true;
                        continue;
                        break;

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


                    case ParameterStart + "hashAlgo":
                        ResetInsideArgs();
                        insideHashAlgorithmToUse = true;
                        continue;
                        break;

                    case ParameterStart + "snapshotDirName":
                        ResetInsideArgs();
                        insideSnapshotDirName = true;
                        continue;
                        break;

                    case ParameterStart + "watchDir":
                        ResetInsideArgs();
                        insideWatchDir = true;
                        continue;
                        break;

                    case ParameterStart + "dumpExprs":
                        ResetInsideArgs();
                        insideCreateWatchExpressionsDumpFile = true;
                        continue;
                        break;

                    case ParameterStart + "dumpName":
                        ResetInsideArgs();
                        insideDumpWatchExpressionsFileName = true;
                        continue;
                        break;


                    case ParameterStart + "ignoreFile":
                    case ParameterStart + "ignoreFiles":
                        ResetInsideArgs();
                        insideFilesToIgnore = true;
                        continue;
                        break;

                    case ParameterStart + "ignoreDir":
                    case ParameterStart + "ignoreDirs":
                        ResetInsideArgs();
                        insideDirsToIgnore = true;
                        continue;
                        break;

                    case ParameterStart + "tempFile":
                    case ParameterStart + "tempFiles":
                        ResetInsideArgs();
                        insideNotUseInMemoryStringBuilderFileForUpdateingDocs = true;
                        continue;
                        break;

                    case ParameterStart + "writeLog":
                        ResetInsideArgs();
                        insideWriteLog = true;
                        continue;
                        break;
                    case ParameterStart + "showLog":
                        ResetInsideArgs();
                        insideShowLog = true;
                        continue;
                        break;

                    case ParameterStart + "config":
                        ResetInsideArgs();
                        insideConfigFileName = true;
                        continue;
                        break;
                }

                if (insideDirs) config.Dirs.Add(arg);
                if (insideFiles) config.Files.Add(arg);

                if (insideRecursiveCheckDirs) config.RecursiveCheckDirs = false;
                if (insideCreateWatchExpressionsDumpFile) config.CreateWatchExpressionsDumpFile = true;
                if (insideWatchDir) config.WatchCodeDirName = arg;
                if (insideSnapshotDirName) config.SnapshotDirName = arg;

                if (insideDumpWatchExpressionsFileName) config.DumpWatchExpressionsFileName = arg;
                if (insideRootDir) config.RootDir = arg;
                if (insideNoReduce) config.ReduceWatchExpressions = false;
                if (insideNoCombineSnapshots) config.CombineSnapshotFiles = false;
                if (insideHashAlgorithmToUse) config.HashAlgorithmToUse = arg;
                if (insideFilesToIgnore) config.FilesToIgnore.Add(arg);
                if (insideDirsToIgnore) config.DirsToIgnore.Add(arg);
                if (insideNotUseInMemoryStringBuilderFileForUpdateingDocs)
                    config.UseInMemoryStringBuilderFileForUpdateingDocs = false;

                if (insideConfigFileName) cmdArgs.ConfigFileNameWithExtension = arg;
                if (insideWriteLog) cmdArgs.WriteLog = true;
                if (insideShowLog) Logger.OutputLogToConsole = true;
                
                if (insideLogLevel)
                {
                    switch (arg.ToLower())
                    {
                        case "none":
                        case "quiet":
                            Logger.LogLevel = LogLevel.None;
                            break;

                        case "info":
                        case "verbose":
                        case "debug":
                            Logger.LogLevel = LogLevel.Info;
                            break;

                        case "warn":
                        case "warning":
                        case "warnings":
                            Logger.LogLevel = LogLevel.Warn;
                            break;

                        case "error":
                        case "errors":
                        case "critical":
                            Logger.LogLevel = LogLevel.Warn;
                            break;

                        default:
                            Logger.LogLevel = LogLevel.Info;
                            Logger.Warn($"unknown log level: {arg}, set to info");
                            break;
                    }
                }
            }

            return (cmdArgs, config);
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
            insideHashAlgorithmToUse = false;
            insideConfigFileName = false;
            insideFilesToIgnore = false;
            insideDirsToIgnore = false;
            insideNotUseInMemoryStringBuilderFileForUpdateingDocs = false;
            insideWriteLog = false;
            insideShowLog = false;
            insideLogLevel = false;
        }
    }
}
