using watchCode.model;

namespace watchCode.helpers
{
    public static class CmdArgsHelper
    {
        private const string ParameterStart = "-";


        private static bool insideInitMainAction = false;
        private static bool insideUpdateAction = false;
        private static bool insideCompareAction = false;
        private static bool insideCompareAndUpdateDocs = false;
        private static bool insideUpdateDocs = false;
        private static bool insideUpdateAllAction = false;
        private static bool insideinsideUpdateDocsOldExpression = false;
        private static bool insideinsideUpdateDocsNewExpression = false;
        private static bool insideClearUnusedSnapshots = false;

        private static bool insideDocsRootDir = false;
        private static bool insideSourcessRootDir = false;

//        private static bool insideSourceDirs = false;
//        private static bool insideSourceFiles = false;


        private static bool insideRecursiveCheckDocDirs = false;
        //private static bool insideRecursiveCheckSourceDirs = false;

        private static bool insideDocFilesToIgnore = false;
        private static bool insideDocDirsToIgnore = false;
        private static bool insideSourceFilesToIgnore = false;
        private static bool insideSourceDirsToIgnore = false;


        private static bool insideCreateWatchExpressionsDumpFile = false;
        private static bool insideWatchDir = false;
        private static bool insideSnapshotDirName = false;
        private static bool insideDumpWatchExpressionsFileName = false;

        private static bool insideHashAlgorithmToUse = false;
        private static bool insideConfigFileName = false;

        private static bool insideWriteLog = false;
        private static bool insideShowLog = false;
        private static bool insideLogLevel = false;

        public static (CmdArgs, Config) ParseArgs(string[] args)
        {
            var cmdArgs = new CmdArgs();
            cmdArgs.MainAction = MainAction.None;

            var config = Config.DefaultConfig;

            foreach (var arg in args)
            {
                //some cases use continue, some break
                //continue if after the argument name follow the actual arguments e.g. -file file1 file2
                //break if the argument name has not arguments e.g. -showLog
                switch (arg)
                {
                    case ParameterStart + "init":
                        ResetInsideArgs();
                        insideInitMainAction = true;
                        cmdArgs.MainAction = MainAction.Init;
                        break;

                    case ParameterStart + "update":
                        ResetInsideArgs();
                        insideUpdateAction = true;
                        cmdArgs.MainAction = MainAction.Update;
                        break;

                    case ParameterStart + "compare":
                        ResetInsideArgs();
                        insideCompareAction = true;
                        cmdArgs.MainAction = MainAction.Compare;
                        break;

                    case ParameterStart + "compareUpdateDocs":
                        ResetInsideArgs();
                        insideCompareAndUpdateDocs = true;
                        break;

                    case ParameterStart + "updateAll":
                        ResetInsideArgs();
                        insideUpdateAllAction = true;
                        cmdArgs.MainAction = MainAction.UpdateAll;
                        break;

                    case ParameterStart + "updateExpr":
                        ResetInsideArgs();
                        insideUpdateDocs = true;
                        cmdArgs.MainAction = MainAction.UpdateExpression;
                        break;


                    case ParameterStart + "oldExp":
                        ResetInsideArgs();
                        insideinsideUpdateDocsOldExpression = true;
                        continue;
                        break;

                    case ParameterStart + "newExp":
                        ResetInsideArgs();
                        insideinsideUpdateDocsNewExpression = true;
                        continue;
                        break;
                    
                    case ParameterStart + "clearUnusedSnapshots":
                        ResetInsideArgs();
                        insideClearUnusedSnapshots = true;
                        break;
                        


                    #region path stuff

                    case ParameterStart + "docsRootDir":
                        ResetInsideArgs();
                        insideDocsRootDir = true;
                        continue;
                        break;

                    case ParameterStart + "sourcesRootDir":
                        ResetInsideArgs();
                        insideSourcessRootDir = true;
                        continue;
                        break;

//                    case ParameterStart + "sourceDir":
//                    case ParameterStart + "sourceDirs":
//                        ResetInsideArgs();
//                        insideSourceDirs = true;
//                        continue;
//                        break;
//
//                    case ParameterStart + "sourceFile":
//                    case ParameterStart + "sourceFiles":
//                        ResetInsideArgs();
//                        insideSourceFiles = true;
//                        continue;
//                        break;


                    case ParameterStart + "ignoreDocFile":
                    case ParameterStart + "ignoreDocFiles":
                        ResetInsideArgs();
                        insideDocFilesToIgnore = true;
                        continue;
                        break;

                    case ParameterStart + "ignoreDocDir":
                    case ParameterStart + "ignoreDocDirs":
                        ResetInsideArgs();
                        insideDocDirsToIgnore = true;
                        continue;
                        break;

                    case ParameterStart + "ignoreSourceFile":
                    case ParameterStart + "ignoreSourceFiles":
                        ResetInsideArgs();
                        insideSourceDirsToIgnore = true;
                        continue;
                        break;

                    case ParameterStart + "ignoreSourceDir":
                    case ParameterStart + "ignoreSourceDirs":
                        ResetInsideArgs();
                        insideSourceFilesToIgnore = true;
                        continue;
                        break;

                    case ParameterStart + "notRecursiveDocDirs":
                        ResetInsideArgs();
                        insideRecursiveCheckDocDirs = true;
                        break;

//                    case ParameterStart + "notRecursiveSourceDirs":
//                        ResetInsideArgs();
//                        insideRecursiveCheckSourceDirs = true;
//                        break;

                    #endregion


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
                        break;

                    case ParameterStart + "dumpName":
                        ResetInsideArgs();
                        insideDumpWatchExpressionsFileName = true;
                        continue;
                        break;


                    case ParameterStart + "writeLog":
                        ResetInsideArgs();
                        insideWriteLog = true;
                        break;
                    case ParameterStart + "showLog":
                        ResetInsideArgs();
                        insideShowLog = true;
                        break;

                    case ParameterStart + "config":
                        ResetInsideArgs();
                        insideConfigFileName = true;
                        continue;
                        break;

                    case ParameterStart + "logLevel":
                        ResetInsideArgs();
                        insideLogLevel = true;
                        continue;
                        break;
                }

                ApplyArg(arg, cmdArgs, config);
            }

            return (cmdArgs, config);
        }

        private static void ApplyArg(string arg, CmdArgs cmdArgs, Config config)
        {
            if (insideDocsRootDir) config.DocFilesDirAbsolutePath = arg;
            if (insideSourcessRootDir) config.SourceFilesDirAbsolutePath = arg;


//            if (insideSourceDirs) config.SourceDirs.Add(arg);
//            if (insideSourceFiles) config.SourceFiles.Add(arg);

            if (insideRecursiveCheckDocDirs) config.RecursiveCheckDocDirs = false;
//            if (insideRecursiveCheckSourceDirs) config.RecursiveCheckSourceDirs = false;

            if (insideDocFilesToIgnore) config.DocFilesToIgnore.Add(arg);
            if (insideDocDirsToIgnore) config.DocDirsToIgnore.Add(arg);

            if (insideSourceFilesToIgnore) config.SourceFilesToIgnore.Add(arg);
            if (insideSourceDirsToIgnore) config.SourceDirsToIgnore.Add(arg);


            if (insideCreateWatchExpressionsDumpFile) config.CreateWatchExpressionsDumpFile = true;
            if (insideWatchDir) config.WatchCodeDirName = arg;
            if (insideSnapshotDirName) config.SnapshotDirName = arg;

            if (insideDumpWatchExpressionsFileName) config.DumpWatchExpressionsFileName = arg;


            if (insideHashAlgorithmToUse) config.HashAlgorithmToUse = arg;

            if (insideConfigFileName) cmdArgs.ConfigFileNameWithExtension = arg;
            if (insideWriteLog) cmdArgs.WriteLog = true;
            if (insideShowLog) Logger.OutputLogToConsole = true;


            if (insideInitMainAction) cmdArgs.MainAction = MainAction.Init;

            if (insideUpdateAction) cmdArgs.TargetWatchExpressions = arg;

            if (insideCompareAction) cmdArgs.MainAction = MainAction.Compare;
            if (insideUpdateAllAction) cmdArgs.MainAction = MainAction.UpdateAll;

            if (insideCompareAndUpdateDocs) cmdArgs.MainAction = MainAction.CompareAndUpdateDocs;

            if (insideClearUnusedSnapshots) cmdArgs.MainAction = MainAction.ClearUnusedSnapshots;
            
            if (insideUpdateDocs) cmdArgs.MainAction = MainAction.UpdateExpression;

            if (insideinsideUpdateDocsOldExpression) cmdArgs.UpdateDocsOldWatchExpression = arg;
            if (insideinsideUpdateDocsNewExpression) cmdArgs.UpdateDocsNewWatchExpression = arg;


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

        private static void ResetInsideArgs()
        {
            insideInitMainAction = false;
            insideUpdateAction = false;
            insideCompareAction = false;
            insideCompareAndUpdateDocs = false;
            insideUpdateAllAction = false;
            insideUpdateDocs = false;
            insideClearUnusedSnapshots = false;

            insideinsideUpdateDocsOldExpression = false;
            insideinsideUpdateDocsNewExpression = false;

            insideDocsRootDir = false;
            insideSourcessRootDir = false;

            insideRecursiveCheckDocDirs = false;
            insideDocFilesToIgnore = false;
            insideDocDirsToIgnore = false;
            insideSourceFilesToIgnore = false;
            insideSourceDirsToIgnore = false;
            insideCreateWatchExpressionsDumpFile = false;
            insideWatchDir = false;
            insideSnapshotDirName = false;
            insideDumpWatchExpressionsFileName = false;
            insideHashAlgorithmToUse = false;
            insideConfigFileName = false;
            insideWriteLog = false;
            insideShowLog = false;
            insideLogLevel = false;
        }
    }
}
