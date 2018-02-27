using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DiffMatchPatch;
using watchCode.helpers;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode
{
    public class Program
    {
        public const int OkReturnCode = 0;
        public const int NotEqualCompareReturnCode = 1;
        public const int NeedToUpdateDocRanges = 2;
        public const int ErrorReturnCode = 100;

        public static Stopwatch Stopwatch = new Stopwatch();
        public static Stopwatch TotalStopwatch = new Stopwatch();

        public static ConsoleColor OkResultBgColor = ConsoleColor.DarkGreen;
        public static ConsoleColor WarningResultBgColor = ConsoleColor.DarkYellow;
        public static ConsoleColor ChangedResultBgColor = ConsoleColor.DarkRed;

        //all strings should have the same length, looks nicer
        public static string OkResultString = "  OK  ";

        public static string AutoUpdateResultString = " AUTO ";
        public static string WarningResultString = " WARN ";
        public static string ChangedResultString = " FAIL ";


        static int Main(string[] args)
        {
            TotalStopwatch.Start();

            Stopwatch.Start();
            Logger.Info($"--- bootstrapping ---");


            DynamicConfig.DocFilesDirAbsolutePath = Directory.GetCurrentDirectory();
            DynamicConfig.SourceFilesDirAbsolutePath = Directory.GetCurrentDirectory();

            var (cmdArgs, config) = CmdArgsHelper.ParseArgs(args);

            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs, config);

            Stopwatch.Stop();
            Logger.Info($"--- end bootstrapping (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");

            int returnCode = Run(cmdArgs, config);

            TotalStopwatch.Stop();

            //Logger.Info("---");
            var message = $"total time: {StopWatchHelper.GetElapsedTime(TotalStopwatch)}";
            Logger.Info(message);
            Console.WriteLine(message);

            return returnCode;
        }


        public static int Run(CmdArgs cmdArgs, Config config)
        {
            int returnCode = OkReturnCode;

//            Logger.OutputLogToConsole = true;


//            cmdArgs.MainAction = MainAction.Init;
//            cmdArgs.MainAction = MainAction.Update;
            //cmdArgs.MainAction = MainAction.Compare;

            //after updating docs all bottom to top snapshots are invalid (ranges)
            //after the docs update we don't know which snapshot belongs to which doc watch expression...


            //cmdArgs.CompareAndUpdateDocs = true;

            config.DocDirsToIgnore.Add(config.WatchCodeDirName);
            Logger.Info($"added dir {DynamicConfig.GetAbsoluteWatchCodeDirPath(config)} to " +
                        $"ignore list because this is the watch code dir");


            if (Config.Validate(config, cmdArgs) == false) //this also checks if all files exists
            {
                Logger.Error("config was invalid, exitting");
                PrintHelp();
                return ErrorReturnCode;
            }


            Stopwatch.Restart();
            Logger.Info("--- getting all watch expressions ---");


            #region --- get all doc files that could contain watch expressions 

            //this also checks if files exists... in case we use it without calling validate before
            var allDocFileInfos = FileIteratorHelper.GetAllFiles(DynamicConfig.DocFilesDirAbsolutePath,
                new List<string>(),
                new List<string>() {"."},
                DynamicConfig.KnownFileExtensionsWithoutExtension.Keys.ToList(), config.RecursiveCheckDocDirs.Value,
                config.DocFilesToIgnore,
                config.DocDirsToIgnore, config.IgnoreHiddenFiles.Value);

            #endregion

            #region --- get all watch expressions

            List<WatchExpression> allWatchExpressions = new List<WatchExpression>();


            foreach (var docFileInfo in allDocFileInfos)
            {
                var watchExpressions = WatchExpressionParseHelper.GetAllWatchExpressions(docFileInfo,
                    DynamicConfig.KnownFileExtensionsWithoutExtension,
                    DynamicConfig.InitWatchExpressionKeywords);

                allWatchExpressions.AddRange(watchExpressions);
            }


            if (allWatchExpressions.Count == 0)
            {
                Logger.Warn("no watch expressions found");
            }

            Stopwatch.Stop();
            Logger.Info(
                $"--- end getting all watch expressions (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");

            #endregion

            //make sure we don't watch the same (source) lines in the same doc file rang...
            //e.g. if a doc file has 1 watch expression with >= 2 expressions watching the same source file range

            //not allowed by syntax
//            allWatchExpressions = allWatchExpressions
//                .DistinctBy(p => p.GetFullIdentifier())
//                .ToList();

//           allWatchExpressions
//               .Where(p => cmdArgs.TargetWatchExpressions.Any(k => k.StartsWith(p.SourceFilePath)))


            #region --- create dump expression file (if needed from cmd args) 

            // ReSharper disable once PossibleInvalidOperationException
            if (config.CreateWatchExpressionsDumpFile.Value)
            {
                DumpWatchExpressionHelper.DumpWatchExpressions(Path.Combine(DynamicConfig.DocFilesDirAbsolutePath,
                        config.WatchCodeDirName, config.DumpWatchExpressionsFileName),
                    allWatchExpressions, true);
            }

            #endregion


            #region --- main part

            Stopwatch.Restart();
            Logger.Info("--- main part ---");

//            Dictionary<WatchExpression, (bool wasEqual, bool needToUpdateRange, Snapshot oldSnapshot, Snapshot
//                newSnapshot)> equalMap;


//            NoReduceWatchExpressionsRun(allWatchExpressions, cmdArgs.MainAction, config,
//                out equalMap,
//                out var initedSnapshots,
//                out var createdAndUpdatedSnapshots);

            Stopwatch.Stop();
            Logger.Info($"--- end main part (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");

            #endregion


            #region --- output / results

            if (cmdArgs.MainAction == MainAction.Init)
            {
                var list = InitActionHandler.InitAction(allWatchExpressions, config);
                InitActionHandler.OutputInitResults(list);
            }
            else if (cmdArgs.MainAction == MainAction.UpdateAll)
            {
                var list = UpdateActionHandler.UpdateAction(allWatchExpressions, config);
                UpdateActionHandler.OutputUpdateResults(list);
            }
            else if (cmdArgs.MainAction == MainAction.Update)
            {
                var targetWatchExpressions = new List<WatchExpression>();

                foreach (var watchExpression in allWatchExpressions)
                {
                    string ident = watchExpression.GetSourceFileLocation();

                    if (cmdArgs.TargetWatchExpressions.Contains(ident)) targetWatchExpressions.Add(watchExpression);
                }

                var list = UpdateActionHandler.UpdateAction(targetWatchExpressions, config);
                UpdateActionHandler.OutputUpdateResults(list);
            }
            else if (cmdArgs.MainAction == MainAction.UpdateExpression)
            {
                var newWatchExpressions = new List<(WatchExpression oldWatchExpression, WatchExpression newWatchExpression)>();

                foreach (var watchExpression in allWatchExpressions)
                {
                    string ident = watchExpression.GetSourceFileLocation();


                    if (ident == cmdArgs.UpdateDocsOldWatchExpression)
                    {
                        var newWatchExpr =
                            WatchExpressionParseHelper.ParsePlainWatchExpression(cmdArgs.UpdateDocsNewWatchExpression);

                        if (newWatchExpr == null)
                        {
                            Logger.Error(
                                $"{cmdArgs.UpdateDocsNewWatchExpression} is not a valid watch expression, cannot update expression {cmdArgs.UpdateDocsOldWatchExpression}");
                            continue;
                        }

                        var newWatchExpression = new WatchExpression(
                            newWatchExpr.Value.sourceFilePath,
                            newWatchExpr.Value.sourceLineRange.Clone(),
                            watchExpression.DocFilePath,
                            watchExpression.DocLineRange.Clone(),
                            watchExpression.UsedCommentFormat
                        );

                        newWatchExpressions.Add((watchExpression, newWatchExpression));
                    }
                }

                var list = UpdateExpressionHandler.UpdateExpressions(newWatchExpressions, config);
                UpdateExpressionHandler.OutputUpdateExpressionResults(list);
                
            }
            else if (cmdArgs.MainAction == MainAction.Compare)
            {
                var equalMap = CompareActionHandler.CompareAction(allWatchExpressions, config);

                CompareActionHandler.OutputCompareResults(equalMap, out var someSnapshotChanged,
                    out var someSnapshotUsedBottomToTopOrSearchLines, false);
            }
            else if (cmdArgs.MainAction == MainAction.CompareAndUpdateDocs)
            {
                var equalMap = CompareActionHandler.CompareAction(allWatchExpressions, config);

                CompareActionHandler.OutputCompareResults(equalMap, out var someSnapshotChanged,
                    out var someSnapshotUsedBottomToTopOrSearchLines, false);


                if (someSnapshotUsedBottomToTopOrSearchLines) returnCode = NeedToUpdateDocRanges;


                //overwrite NeedToUpdateDocRanges because not equal is more important
                if (someSnapshotChanged) returnCode = NotEqualCompareReturnCode;


                if (someSnapshotUsedBottomToTopOrSearchLines)
                {
                    UpdateDocsHandler.UpdateDocCommentWatchExpressions(equalMap, config);
                }
                else if (someSnapshotChanged)
                {
                    //TODO what to output?
                }
                else
                {
                    var message = "No need to update doc files";
                    Console.WriteLine(message);
                    Logger.Info($"{message}, skipping");
                }
            }
            else if (cmdArgs.MainAction == MainAction.ClearUnusedSnapshots)
            {
                var clearedSnapshots = ClearUnusedSpansotsActionHandler.ClearUnusedSpansots(allWatchExpressions, config);
                ClearUnusedSpansotsActionHandler.OutputClearResults(clearedSnapshots);
                
            }
            else
            {
                Console.WriteLine("No action specified, nothing to do");
            }

            #endregion

            if (cmdArgs.WriteLog) Logger.WriteLog(config);

            return returnCode;
        }


  

        static void PrintHelp()
        {
//            Console.WriteLine("seems that you need to update the line range because" +
//                              "some lines in some files were inserted...");
//            Console.WriteLine("you can run the command compare and update docs to automatically update" +
//                              "the line ranges...");
//
//            Console.ForegroundColor = ConsoleColor.Red;
//            Console.WriteLine("...this will rewrite the watch expression comments and delete every text " +
//                              "before and after the watch expression inside the corresponding comment");
//            Console.ResetColor();
        }
    }
}
