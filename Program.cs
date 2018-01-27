using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using watchCode.helpers;
using watchCode.model;

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


            DynamicConfig.AbsoluteRootDirPath = Directory.GetCurrentDirectory();

            var (cmdArgs, config) = CmdArgsHelper.ParseArgs(args);

            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs, config);

            Stopwatch.Stop();
            Logger.Info($"--- end bootstrapping (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");

            int returnCode = Run(cmdArgs, config);

            TotalStopwatch.Stop();

            //Logger.Info("---");
            Logger.Info($"total time: {StopWatchHelper.GetElapsedTime(TotalStopwatch)}");

            return returnCode;
        }


        public static int Run(CmdArgs cmdArgs, Config config)
        {
            int returnCode = OkReturnCode;

//            Logger.OutputLogToConsole = true;


//            cmdArgs.MainAction = MainAction.Init;
//            cmdArgs.MainAction = MainAction.Update;
            cmdArgs.MainAction = MainAction.Compare;

            //after updating docs all bottom to top snapshots are invalid (ranges)
            //after the docs update we don't know which snapshot belongs to which doc watch expression...


            cmdArgs.CompareAndUpdateDocs = true;


            config.DirsToIgnore.Add(DynamicConfig.GetAbsoluteWatchCodeDirPath(config));
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
            var allDocFileInfos = FileIteratorHelper.GetAllFiles(config.Files, config.Dirs,
                DynamicConfig.KnownFileExtensionsWithoutExtension.Keys.ToList(), config.RecursiveCheckDirs.Value,
                config.FilesToIgnore,
                config.DirsToIgnore, config.IgnoreHiddenFiles.Value);

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

            allWatchExpressions = allWatchExpressions
                .DistinctBy(p => p.GetFullIdentifier())
                .ToList();


            #region --- create dump expression file (if needed from cmd args) 

            // ReSharper disable once PossibleInvalidOperationException
            if (config.CreateWatchExpressionsDumpFile.Value)
            {
                DumpWatchExpressionHelper.DumpWatchExpressions(Path.Combine(DynamicConfig.AbsoluteRootDirPath,
                        config.WatchCodeDirName, config.DumpWatchExpressionsFileName),
                    allWatchExpressions, true);
            }

            #endregion

            #region --- main part

            Stopwatch.Restart();
            Logger.Info("--- main part ---");

            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap;


            NoReduceWatchExpressionsRun(allWatchExpressions, cmdArgs.MainAction, config,
                out equalMap,
                out var initedSnapshots,
                out var createdAndUpdatedSnapshots);

            Stopwatch.Stop();
            Logger.Info($"--- end main part (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");

            #endregion


            #region --- output / results

            if (cmdArgs.MainAction == MainAction.Init)
            {
                OutputInitResults(initedSnapshots);
            }
            else if (cmdArgs.MainAction == MainAction.Update)
            {
                OutputUpdateResults(createdAndUpdatedSnapshots);
            }
            else if (cmdArgs.MainAction == MainAction.Compare)
            {
                OutputCompareResults(equalMap, out var someSnapshotChanged,
                    out var someSnapshotUsedBottomToTopOrSearchLines, false);

                if (someSnapshotUsedBottomToTopOrSearchLines) returnCode = NeedToUpdateDocRanges;


                //overwrite NeedToUpdateDocRanges because not equal is more important
                if (someSnapshotChanged) returnCode = NotEqualCompareReturnCode;


                if (cmdArgs.CompareAndUpdateDocs)
                {
                    UpdateDocCommentWatchExpressions(equalMap, config);
                }
            }

            #endregion

            if (cmdArgs.WriteLog) Logger.WriteLog(config);

            return returnCode;
        }

        /// <summary>
        /// </summary>
        /// <param name="allWatchExpressions"></param>
        /// <param name="mainAction"></param>
        /// <param name="config"></param>
        /// <param name="equalMap">one entry for every item in allWatchExpressions</param>
        /// <param name="initedSnapshots"></param>
        /// <param name="createdAndUpdatedSnapshots"></param>
        private static void NoReduceWatchExpressionsRun(List<WatchExpression> allWatchExpressions,
            MainAction mainAction,
            Config config,
            out Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            out List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)> initedSnapshots,
            out List<(WatchExpression watchExpression, Snapshot snapshot)> createdAndUpdatedSnapshots
        )
        {
            var snapshotsDictionary =
                new Dictionary<string, List<(WatchExpression watchExpression, Snapshot snapshot)>>();

            var alreadyReadSnapshots = new Dictionary<string, List<Snapshot>>();

            //stores the equal result for every watch expression
            equalMap =
                new Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)>();


            initedSnapshots =
                new List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)>();


            //update includes init...
            createdAndUpdatedSnapshots = new List<(WatchExpression watchExpression, Snapshot snapshot)>();

            foreach (var watchExpression in allWatchExpressions)
            {
                Logger.Info($"evaluating watch expression in doc file: {watchExpression.GetDocumentationLocation()}, " +
                            $"watching source file: {watchExpression.GetSourceFileLocation()}");

                if (mainAction == MainAction.Init)
                {
                    PerformInitAction(watchExpression, config, snapshotsDictionary, alreadyReadSnapshots,
                        initedSnapshots);
                }
                else if (mainAction == MainAction.Update)
                {
                    PerformUpdateAction(watchExpression, config, snapshotsDictionary, createdAndUpdatedSnapshots);
                }
                else if (mainAction == MainAction.Compare)
                {
                    PerformCompareAction(watchExpression, config, alreadyReadSnapshots, equalMap);
                }
            }


            if (mainAction == MainAction.Init || mainAction == MainAction.Update)
            {
                // ReSharper disable once PossibleInvalidOperationException
                if (config.CombineSnapshotFiles.Value)
                {
                    //we haven't written any snapshots yet...
                    BulkWriteCapturedSnapshots(config, snapshotsDictionary, mainAction, initedSnapshots,
                        createdAndUpdatedSnapshots);
                }
                //else already created in EvaulateSingleWatchExpression
            }
        }

        private static void BulkWriteCapturedSnapshots(Config config,
            Dictionary<string, List<(WatchExpression watchExpression, Snapshot snapshot)>> snapshotsDictionary,
            MainAction mainAction,
            List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)> initedSnapshots,
            List<(WatchExpression watchExpression, Snapshot snapshot)> createdAndUpdatedSnapshots
        )
        {
            Logger.Info($"----- bulk writing snapshots -----");

            //but now we know all watch expressions to combine for every file

            //we would write them one after another in the target file... then then wrap them by []
            //this should be equal to the json transform

            foreach (var pair in snapshotsDictionary)
            {
                //pair.Value.Count > 0 is checked in SaveSnapshots with message
                bool created =
                    SnapshotWrapperHelper.SaveSnapshots(pair.Value.Select(p => p.snapshot).ToList(), config);


                if (created)
                {
                    (WatchExpression watchExpression, Snapshot snapshot) firstTuple = pair.Value.First();
                    Logger.Info(
                        $"snapshot for all watch expressions in for doc " +
                        $"file: {firstTuple.watchExpression.GetDocumentationLocation()} was created at: " +
                        $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), firstTuple.watchExpression, true)}");
                }
                else
                {
                    if (mainAction == MainAction.Init)
                    {
                        //we need to update initedSnapshots properly
                        //because we captured the snapshot but was not saved

                        foreach (var tuple in pair.Value)
                        {
                            var notSavedSnapshotTuple =
                                initedSnapshots.FirstOrDefault(p => p.snapshot == tuple.snapshot);

                            if (notSavedSnapshotTuple.Equals(
                                default((WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)
                                )))
                            {
                                Logger.Error($"could not find inited snapshot to set result to failed " +
                                             $"(because bulk write failed), " +
                                             $"doc file: {tuple.watchExpression.GetDocumentationLocation()}, " +
                                             $"source file: {tuple.watchExpression.GetSourceFileLocation()}");
                                continue;
                            }

                            notSavedSnapshotTuple.snapshot = null;
                        }
                    }
                    else if (mainAction == MainAction.Update)
                    {
                        //we need to set createdAndUpdatedSnapshots properly
                        //because we captured the snapshot but was not saved
                        foreach (var tuple in pair.Value)
                        {
                            var notSavedSnapshotTuple =
                                createdAndUpdatedSnapshots.FirstOrDefault(p => p.snapshot == tuple.snapshot);

                            if (notSavedSnapshotTuple.Equals(
                                default((WatchExpression watchExpression, Snapshot snapshot)
                                )))
                            {
                                Logger.Error($"could not find updated snapshot to set result to failed " +
                                             $"(because bulk write failed), " +
                                             $"doc file: {tuple.watchExpression.GetDocumentationLocation()}, " +
                                             $"source file: {tuple.watchExpression.GetSourceFileLocation()}");
                                continue;
                            }

                            notSavedSnapshotTuple.snapshot = null;
                        }
                    }
                }
            }

            Logger.Info($"----- end bulk writing snapshots -----");
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="watchExpression"></param>
        /// <param name="config"></param>
        /// <param name="snapshotsDictionary"></param>
        /// <param name="alreadyReadSnapshots"></param>
        /// <param name="capturedSnapshots">
        /// if snapshot == null && snapshotAlreadyExists -> we haven't captured snapshot because it already exists
        /// if snapshot == null && snapshotAlreadyExists == false -> error capturing snapshot
        /// if snapshot != null && snapshotAlreadyExists == true -> should not happen
        /// if snapshot != null && snapshotAlreadyExists == false -> captured snapshot
        /// </param>
        private static void PerformInitAction(WatchExpression watchExpression, Config config,
            Dictionary<string, List<(WatchExpression watchExpression, Snapshot snapshot)>> snapshotsDictionary,
            Dictionary<string, List<Snapshot>> alreadyReadSnapshots,
            List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)> capturedSnapshots
        )
        {
            #region --- init option

            // ReSharper disable once PossibleInvalidOperationException
            if (config.CombineSnapshotFiles.Value)
            {
                //because we store combined snapshots we read all snapshots in the file
                //actually we would only need to read until we found the right snapshot but we assume
                //that we almost every time need to read all snapshots from the file anyway

                //so store the already read snapshots

                bool singleSnapshotExists = false;

                if (alreadyReadSnapshots.TryGetValue(watchExpression.WatchExpressionFilePath,
                    out var alreayReadSnapshots))
                {
                    if (alreayReadSnapshots.Any(p => p.LineRange == watchExpression.LineRange))
                        singleSnapshotExists = true;
                }
                else
                {
                    singleSnapshotExists =
                        SnapshotHelper.SnapshotCombinedExists(
                            DynamicConfig.GetAbsoluteSnapShotDirPath(config),
                            watchExpression, out var readSnapshots);

                    if (singleSnapshotExists)
                    {
                        alreadyReadSnapshots.Add(watchExpression.WatchExpressionFilePath, readSnapshots);
                    }
                }

                if (singleSnapshotExists)
                {
                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"already exists at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), watchExpression, true)}" +
                                $", skipping");

                    capturedSnapshots.Add((watchExpression, null, true));

                    return;
                }
                else
                {
                    //do not save the snapshot yet... maybe we get snapshots for the same file...
                    // ReSharper disable once PossibleInvalidOperationException
                    var newSnapshot =
                        SnapshotWrapperHelper.CreateSnapshot(watchExpression);

                    //inner func should have reported the error
                    if (newSnapshot == null)
                    {
                        capturedSnapshots.Add((watchExpression, null, false));
                        return;
                    }


                    if (snapshotsDictionary.TryGetValue(watchExpression.WatchExpressionFilePath,
                        out var snapshots))
                    {
                        //add the snapshot to the others for the same source file (to bulk write)
                        snapshots.Add((watchExpression, newSnapshot));
                    }
                    else
                    {
                        snapshotsDictionary.Add(watchExpression.WatchExpressionFilePath,
                            new List<( WatchExpression watchExpression, Snapshot snapshot)>()
                            {
                                (watchExpression, newSnapshot)
                            });
                    }

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"was added to bulk save: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), watchExpression, true)}");

                    capturedSnapshots.Add((watchExpression, newSnapshot, false));
                }

                return;
            }

            //only create snapshots if they not exit yet
            //do not touch old snapshots
            bool snapshotExists =
                SnapshotHelper.SnapshotExists(
                    DynamicConfig.GetAbsoluteSnapShotDirPath(config),
                    watchExpression);

            if (snapshotExists)
            {
                //do not update, everything ok here

                Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                            $"already exists at: " +
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), watchExpression, true)}" +
                            $", skipping");

                capturedSnapshots.Add((watchExpression, null, true));
                return;
            }

            //create and save new snapshot
            // ReSharper disable once PossibleInvalidOperationException
            bool created = SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, config, out var snapshot);

            if (created)
            {
                capturedSnapshots.Add((watchExpression, snapshot, false));
            }
            else
            {
                capturedSnapshots.Add((watchExpression, null, false));
            }

            Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                        $"was created at: " +
                        $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), watchExpression, true)}");

            #endregion
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="watchExpression"></param>
        /// <param name="config"></param>
        /// <param name="snapshotsDictionary"></param>
        /// <param name="capturedSnapshots">
        /// if snapshot == null -> error capturing snapshot
        /// if snapshot != null -> captured snapshot
        /// </param>
        private static void PerformUpdateAction(WatchExpression watchExpression, Config config, Dictionary<string,
                List<(WatchExpression watchExpression, Snapshot snapshot)>> snapshotsDictionary,
            List<(WatchExpression watchExpression, Snapshot snapshot)> capturedSnapshots)
        {
            #region --- update option

            //update all snapshots ...
            //update old snapshots
            //create new snapshots

            // ReSharper disable once PossibleInvalidOperationException
            if (config.CombineSnapshotFiles.Value)
            {
                // ReSharper disable once PossibleInvalidOperationException
                var newSnapshot =
                    SnapshotWrapperHelper.CreateSnapshot(watchExpression);

                if (newSnapshot != null &&
                    snapshotsDictionary.TryGetValue(watchExpression.WatchExpressionFilePath, out var snapshots))
                {
                    //add the snapshot to the others for the same source file (to bulk write)
                    snapshots.Add((watchExpression, newSnapshot ));
                }
                else if (newSnapshot != null)
                {
                    snapshotsDictionary.Add(watchExpression.WatchExpressionFilePath,
                        new List<(WatchExpression watchExpression, Snapshot snapshot )>()
                        {
                            (watchExpression, newSnapshot )
                        });
                }
                else // newSnapshot == null
                {
                    //error already produced in CreateSnapshot

                    capturedSnapshots.Add((watchExpression, null));
                    return;
                }

                capturedSnapshots.Add((watchExpression, newSnapshot));

                Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                            $"was added at: " +
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), watchExpression, true)}");

                return;
            }

            //create and save new snapshot
            // ReSharper disable once PossibleInvalidOperationException
            bool created = SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, config, out var snapshot);

            if (created)
            {
                capturedSnapshots.Add((watchExpression, snapshot));

                Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                            $"was saved at: " +
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config), watchExpression, true)}");
            }
            else
            {
                capturedSnapshots.Add((watchExpression, null));

                Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                            $"was not saved");
            }

            #endregion
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="watchExpression"></param>
        /// <param name="config"></param>
        /// <param name="alreadyReadSnapshots"></param>
        private static void PerformCompareAction(WatchExpression watchExpression, Config config,
            Dictionary<string, List<Snapshot>> alreadyReadSnapshots,
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap)
        {
            #region --- compare option

            // ReSharper disable once PossibleInvalidOperationException
            string oldSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                DynamicConfig.GetAbsoluteSnapShotDirPath(config),
                watchExpression, config.CombineSnapshotFiles.Value);

            Snapshot oldSnapshot;

            if (config.CombineSnapshotFiles.Value)
            {
                //because we store combined snapshots we need to read every snapshot from the file anyway...
                //so store them

                if (alreadyReadSnapshots.TryGetValue(watchExpression.WatchExpressionFilePath,
                    out var alreayReadSnapshots))
                {
                    if (alreayReadSnapshots == null)
                    {
                        oldSnapshot = null;
                    }
                    else
                    {
                        oldSnapshot =
                            alreayReadSnapshots.FirstOrDefault(p => p.LineRange == watchExpression.LineRange);
                    }
                }
                else
                {
                    List<Snapshot> oldSnapshots = SnapshotWrapperHelper.ReadSnapshots(oldSnapshotPath);
                    //oldSnapshots can be null!! but we need to know if we already read the file.. so keep null
                    alreadyReadSnapshots.Add(watchExpression.WatchExpressionFilePath, oldSnapshots);


                    oldSnapshot = oldSnapshots?.FirstOrDefault(p =>
                        p.LineRange == watchExpression.LineRange || (
                            p.ReversedLineRange != null &&
                            DocsHelper.GetNewLineRangeFromReverseLineRange(p) ==
                            watchExpression.LineRange));
                }
            }
            else
            {
                oldSnapshot = SnapshotWrapperHelper.ReadSnapshot(oldSnapshotPath);
            }


            bool areEqual;

            Snapshot newSnapshot = null;


            if (oldSnapshot == null)
            {
                //normally this can't happen ... because we created the snapshot...
                //but on error or if someone deleted the snapshot in the snapshot dir...
                equalMap.Add(watchExpression, (false, null, null));

                return;
            }
            else if (oldSnapshot.LineRange == null || oldSnapshot.ReversedLineRange == null)
            {
                //old snapshot was for the whote file or we don't
                //know reversed lines ... so take normal snapshot

                // ReSharper disable once PossibleInvalidOperationException
                newSnapshot =
                    SnapshotWrapperHelper.CreateSnapshot(watchExpression);

                areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);
            }
            else
            {
                //the old snapshot exists & has the same line ranges...

                // ReSharper disable once PossibleInvalidOperationException
                newSnapshot =
                    SnapshotWrapperHelper.CreateSnapshotBasedOnOldSnapshot(watchExpression, oldSnapshot,
                        out var snapshotsWereEqual);

                areEqual = snapshotsWereEqual;
            }


            if (newSnapshot == null)
            {
                equalMap.Add(watchExpression, (false, oldSnapshot, null));
            }
            else
            {
                //already checked in CreateSnapshotBasedOnOldSnapshot
                equalMap.Add(watchExpression,
                    (areEqual, oldSnapshot, newSnapshot));
            }

            #endregion
        }


        private static void OutputCompareResults(
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            out bool someSnapshotChanged, out bool someSnapshotUsedBottomToTopOrSearchLines, bool suppressOutput)
        {
            Stopwatch.Restart();
            Logger.Info("--- compare results ---");
            Console.WriteLine($"{new string('-', OkResultString.Length)} compare results");

            WatchExpression watchExpression;

            someSnapshotChanged = false;
            someSnapshotUsedBottomToTopOrSearchLines = false;

            int autoUpdatePossibleCount = 0;
            int noAutoUpdatePossibleCount = 0;
            int notChangedCount = 0;

            foreach (var pair in equalMap)
            {
                var tuple = pair.Value;
                watchExpression = pair.Key;

                if (tuple.wasEqual)
                {
                    if (tuple.newSnapshot.TriedBottomOffset)
                    {
                        //user inserted lines before the watched lines
                        someSnapshotUsedBottomToTopOrSearchLines = true;

                        if (suppressOutput == false)
                        {
                            Console.BackgroundColor = WarningResultBgColor;
                            Console.Write(WarningResultString);
                            Console.ResetColor();
                            var message =
                                $" {watchExpression.GetDocumentationLocation()} -> {watchExpression.GetSourceFileLocation()} [needs to be updated, used bottom offset]";
                            Console.WriteLine(message);

                            Logger.Info(message);

                            autoUpdatePossibleCount++;
                        }
                    }
                    else if (tuple.newSnapshot.TriedSearchFileOffset)
                    {
                        someSnapshotUsedBottomToTopOrSearchLines = true;

                        //user inserted lines before and after the watched lines --> line range is totaly wrong now

                        if (suppressOutput == false)
                        {
                            Console.BackgroundColor = WarningResultBgColor;
                            Console.Write(WarningResultString);
                            Console.ResetColor();
                            var message =
                                $" {watchExpression.GetDocumentationLocation()} -> {watchExpression.GetSourceFileLocation()} [needs to be updated, used search]";
                            Console.WriteLine(message);

                            Logger.Info(message);

                            autoUpdatePossibleCount++;
                        }
                    }
                    else //nothing changed
                    {
                        Console.BackgroundColor = OkResultBgColor;
                        Console.Write(OkResultString);
                        Console.ResetColor();
                        var message =
                            $" {watchExpression.GetDocumentationLocation()} -> {watchExpression.GetSourceFileLocation()}";
                        Console.WriteLine(message);

                        Logger.Info(message);

                        notChangedCount++;
                    }
                }
                else
                {
                    //could not locate lines... --> something changed in the source file

                    someSnapshotChanged = true;

                    if (suppressOutput == false)
                    {
                        Console.BackgroundColor = ChangedResultBgColor;
                        Console.Write(ChangedResultString);
                        Console.ResetColor();

                        string path = DynamicConfig.GetAbsoluteFilePath(watchExpression.WatchExpressionFilePath);
                        var sourceFileExists = File.Exists(path);

                        if (sourceFileExists)
                        {
                            if (tuple.oldSnapshot == null)
                            {
                                var message =
                                    $" {watchExpression.GetDocumentationLocation()} -> {watchExpression.GetSourceFileLocation()} [no old snapshot found]";
                                Console.WriteLine(message);

                                Logger.Info(message);

                                noAutoUpdatePossibleCount++;
                            }
                            else
                            {
                                var message =
                                    $" {watchExpression.GetDocumentationLocation()} -> {watchExpression.GetSourceFileLocation()} [changed]";
                                Console.WriteLine(message);

                                Logger.Info(message);

                                noAutoUpdatePossibleCount++;
                            }
                        }
                        else
                        {
                            var message =
                                $" {watchExpression.GetDocumentationLocation()} -> {watchExpression.GetSourceFileLocation()} [deleted/renamed source file or no permission to access]";
                            Console.WriteLine(message);

                            Logger.Info(message);

                            noAutoUpdatePossibleCount++;
                        }
                    }
                }
            }

            if (suppressOutput == false)
            {
                if (someSnapshotChanged == false && someSnapshotUsedBottomToTopOrSearchLines == false)
                {
                    Console.ForegroundColor = OkResultBgColor;
                    var message = "all doc files are up to date!";
                    Console.WriteLine(message);
                    Logger.Info(message);
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ChangedResultBgColor;
                    var message = "some doc files need to be updated!";
                    Console.WriteLine(message);
                    Logger.Info(message);
                    Console.ResetColor();

                    //TODO more colors!! maybe something with if > 0 ?
                    Console.WriteLine(
                        $"{noAutoUpdatePossibleCount} expressions changed and need to be updated manually");
                    Console.WriteLine(
                        $"{autoUpdatePossibleCount} expressions changed but can be updated automatically");
                    Console.WriteLine($"{notChangedCount} expressions hasn't changed");
                    
                }
            }

            Stopwatch.Stop();
            Logger.Info($"--- end compare results (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");
        }


        private static void OutputInitResults(
            List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)> initedSnapshots)
        {
            Stopwatch.Restart();
            Logger.Info("--- init results ---");
            Console.WriteLine($"{new string('-', OkResultString.Length)} init results");

            foreach (var tuple in initedSnapshots)
            {
                if (tuple.snapshot == null && tuple.snapshotAlreadyExists)
                {
                    Console.BackgroundColor = OkResultBgColor;
                    Console.Write(OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot already exists]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else if (tuple.snapshot == null && tuple.snapshotAlreadyExists == false)
                {
                    Console.BackgroundColor = ChangedResultBgColor;
                    Console.Write(ChangedResultBgColor);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [could not create snapshot]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else if (tuple.snapshot != null && tuple.snapshotAlreadyExists)
                {
                    Logger.Info(
                        "should/cannot happen in init mode ... snapshot was created but already existed ... but we do not overwrite in init mode ...");

                    //should/cannot happen
                    Console.BackgroundColor = ChangedResultBgColor;
                    Console.Write(ChangedResultBgColor);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [internal error occured!]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else //if (tuple.snapshot != null && tuple.snapshotAlreadyExists == false)
                {
                    Console.BackgroundColor = OkResultBgColor;
                    Console.Write(OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot created]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
            }

            Stopwatch.Stop();
            Logger.Info($"--- end init results (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="createdOrUpdateSnapshots">only the created snapshots, thus we also need the allWatchExpressions to know which we don't updated</param>
        private static void OutputUpdateResults(
            List<(WatchExpression watchExpression, Snapshot snapshot)> createdOrUpdateSnapshots)
        {
            Stopwatch.Restart();
            Logger.Info("--- update snapshot results ---");
            Console.WriteLine($"{new string('-', OkResultString.Length)} update results");

            foreach (var tuple in createdOrUpdateSnapshots)
            {
                if (tuple.snapshot == null)
                {
                    Console.BackgroundColor = ChangedResultBgColor;
                    Console.Write(ChangedResultBgColor);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot could not be updated]";

                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else
                {
                    Console.BackgroundColor = OkResultBgColor;
                    Console.Write(OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot updated]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
            }

            Stopwatch.Stop();
            Logger.Info($"--- end update snapshot results (took {StopWatchHelper.GetElapsedTime(Stopwatch)}) ---");
        }


        private static void UpdateDocCommentWatchExpressions(
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            Config config)
        {
            Stopwatch.Restart();
            Logger.Info("--- auto updating docs ---");
            Console.WriteLine($"{new string('-', OkResultString.Length)} update doc files");

            //if not equal we may recover via searching or bottom offset
            //but if we have >= 2 watch expressions inside 1 comment we need to rewrite the comment
            //and update only e.g. 1 epxression so get all expressions in this comment (group by doc location)

            var groupedWatchExpressions = equalMap
                    .GroupBy(p => p.Key.GetDocumentationLocation())
                    .ToList()
                ;

            //the list of all updated watch expressions (where the line range changed)
            //because we changed the line range the stored snapshots are not invalid (because they have the old
            //line range ... so re created the snapshot for those where the line range changed)

            var allUpdateWatchExpressionsList =
                new List<(WatchExpression oldWatchExpression, WatchExpression updatedWatchExpression)>();


            #region update line range

            foreach (var group in groupedWatchExpressions)
            {
                //tuple contains all expressions in the watch expression comment
                var tuples = group.ToList();

                //if at least one expression in the watch expression comment has changed we need to updat the comment

                //we need equal --> else we need manual update

                // equal && TriedBottomOffset == false && TriedSearchFileOffset == false --> nothing changed
                // equal && TriedBottomOffset == true && TriedSearchFileOffset == false --> update range from 
                //     bottom offset
                // equal && TriedBottomOffset == false && TriedSearchFileOffset == true --> update range from search

                //so we can skip if nothing changed or we cannot update automatically  
                //so we can skip if...
                if (tuples.All(p =>
                    (p.Value.wasEqual && p.Value.newSnapshot.TriedBottomOffset == false &&
                     p.Value.newSnapshot.TriedSearchFileOffset == false) //...nothing changed
                    ||
                    p.Value.wasEqual == false)) //...we cannot update automatically  
                {
                    foreach (var tuple in tuples)
                    {
                        if (tuple.Value.wasEqual)
                        {
                            Console.BackgroundColor = OkResultBgColor;
                            Console.Write(OkResultString);
                            Console.ResetColor();

                            var message =
                                $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [not changed]";
                            Console.WriteLine(message);

                            Logger.Info(message);
                        }
                        else
                        {
                            Console.BackgroundColor = ChangedResultBgColor;
                            Console.Write(ChangedResultString);
                            Console.ResetColor();
                            var message =
                                $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [no auto update possible, some line content changed?]";
                            Console.WriteLine(message);

                            Logger.Info(message);
                        }
                    }

                    continue;
                }


                //here at least 1 in the same doc location (and watch expression position) changed

                //so update the whole expression (keep the not changed values but 
                //update where TriedBottomOffset or TriedSearchFileOffset)

                var expressionAndSnapshotTuples = tuples
                    .Select(p => (p.Key, p.Value.wasEqual, p.Value.newSnapshot)).ToList();

                /*
                 * case 1:
                 *     we used bottom to top (reverse line range) & found lines --> UsedBottomOffset == true
                 * 
                 * --> we need to update the line range
                 *
                 * case 2:
                 *     we searched the file for the lines & found them --> UsedSearchFileOffset == true
                 *
                 * --> we need update the line range... luckily the newSnapshot has the right line range
                 */
                var updated = DocsHelper.UpdateWatchExpressionInDocFile(expressionAndSnapshotTuples,
                    DynamicConfig.KnownFileExtensionsWithoutExtension, DynamicConfig.InitWatchExpressionKeywords,
                    config, out var updateWatchExpressionsList);


                allUpdateWatchExpressionsList.AddRange(updateWatchExpressionsList);

                foreach (var tuple in tuples)
                {
                    if (tuple.Value.wasEqual)
                    {
                        var updateTuple =
                            updateWatchExpressionsList.FirstOrDefault(p =>
                                p.oldWatchExpression == tuple.Key);


                        if (updated && !updateTuple.Equals(
                                default((WatchExpression oldWatchExpression, WatchExpression updateWatchExpression))))
                        {
                            Console.BackgroundColor = OkResultBgColor;
                            Console.Write(AutoUpdateResultString);
                            Console.ResetColor();

                            var message =
                                $" {tuple.Key.GetDocumentationLocation()} -> new: " +
                                $"{updateTuple.updateWatchExpression.GetSourceFileLocation()} (old: " +
                                $"{updateTuple.oldWatchExpression.GetSourceFileLocation()}) [auto updated]";
                            Console.WriteLine(message);

                            Logger.Info(message);
                        }
                        else if (updated && updateTuple.Equals(
                                     default((WatchExpression oldWatchExpression, WatchExpression updateWatchExpression)
                                     )))
                        {
                            //snapshots were equal and don't need to be updated (line range)
                            //but because this is part of a watch expression where SOME other expression needs to 
                            //be updated this was also rewritten

                            Console.BackgroundColor = OkResultBgColor;
                            Console.Write(OkResultString);
                            Console.ResetColor();

                            var message =
                                $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [not changed]";
                            Console.WriteLine(message);

                            Logger.Info(message);
                        }
                        else
                        {
                            Console.BackgroundColor = ChangedResultBgColor;
                            Console.Write(ChangedResultString);
                            Console.ResetColor();

                            var message =
                                $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [error during auto update]";
                            Console.WriteLine(message);

                            Logger.Info(message);
                        }
                    }
                    else
                    {
                        Console.BackgroundColor = ChangedResultBgColor;
                        Console.Write(ChangedResultString);
                        Console.ResetColor();
                        var message =
                            $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [no auto update possible, some line content changed?]";
                        Console.WriteLine(message);

                        Logger.Info(message);
                    }
                }
            }

            #endregion


            #region update snapshots where line range changed

            if (allUpdateWatchExpressionsList.Count != 0)
            {
                var localStopWatch = new Stopwatch();
                localStopWatch.Start();
                Logger.Info("----- auto updating snapshots (where the watch expression line range changed)");


                var newWatchExpressions = allUpdateWatchExpressionsList.Select(p => p.updatedWatchExpression).ToList();

                NoReduceWatchExpressionsRun(newWatchExpressions, MainAction.Update, config,
                    out equalMap,
                    out var initedSnapshots,
                    out var createdAndUpdatedSnapshots);

                localStopWatch.Stop();
                Logger.Info(
                    $"----- end auto updating snapshots (took {StopWatchHelper.GetElapsedTime(localStopWatch)})");

                OutputUpdateResults(createdAndUpdatedSnapshots);
            }

            #endregion


            Stopwatch.Stop();
            Logger.Info($"--- end updating docs (took {StopWatchHelper.GetElapsedTime(Stopwatch)})---");
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
