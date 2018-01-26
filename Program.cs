using System;
using System.Collections.Generic;
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

        static int Main(string[] args)
        {
            DynamicConfig.AbsoluteRootDirPath = Directory.GetCurrentDirectory();

            var (cmdArgs, config) = CmdArgsHelper.ParseArgs(args);

            Logger.OutputLogToConsole = true;

            return Run(cmdArgs, config);
        }


        public static int Run(CmdArgs cmdArgs, Config config)
        {
            int returnCode = OkReturnCode;
            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs, config);

            //cmdArgs.Init = true;
//            cmdArgs.Update = true;
            cmdArgs.Compare = true;

            //after updating docs all bottom to top snapshots are invalid (ranges)
            //after the docs update we don't know which snapshot belongs to which doc watch expression...

           

            //cmdArgs.CompareAndUpdateDocs = true;


            config.DirsToIgnore.Add(DynamicConfig.GetAbsoluteWatchCodeDirPath(config.WatchCodeDirName));
            Logger.Info($"added dir {DynamicConfig.GetAbsoluteWatchCodeDirPath(config.WatchCodeDirName)} to " +
                        $"ignore list because this is the watch code dir");


            if (Config.Validate(config, cmdArgs) == false) //this also checks if all files exists
            {
                Logger.Error("config was invalid, exitting");
                PrintHelp();
                return ErrorReturnCode;
            }


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

            #endregion


            if (allWatchExpressions.Count == 0)
            {
                Logger.Warn("no watch expressions found");
            }

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


            //--- main part


            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap;


            NoReduceWatchExpressionsRun(allWatchExpressions, cmdArgs, config, out equalMap);

            if (cmdArgs.Compare)
            {
                OutputEqualsResults(equalMap, out var someSnapshotChanged, out var someSnapshotUsedBottomToTopLines,
                    false);

                if (someSnapshotUsedBottomToTopLines)
                {
                    Console.WriteLine("seems that you need to update the line range because" +
                                      "some lines in some files were inserted...");
                    Console.WriteLine("you can run the command compare and update docs to automatically update" +
                                      "the line ranges...");

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("...this will rewrite the watch expression comments and delete every text " +
                                      "before and after the watch expression inside the corresponding comment");
                    Console.ResetColor();

                    returnCode = NeedToUpdateDocRanges;
                }
                else if (someSnapshotChanged)
                {
                    returnCode = NotEqualCompareReturnCode;
                }

                if (cmdArgs.CompareAndUpdateDocs)
                {
                    UpdateDocCommentWatchExpressions(equalMap, config);
                }
            }

            if (cmdArgs.WriteLog) Logger.WriteLog(config);


            return returnCode;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="allWatchExpressions"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="config"></param>
        /// <param name="equalMap">one entry for every item in allWatchExpressions</param>
        private static void NoReduceWatchExpressionsRun(List<WatchExpression> allWatchExpressions, CmdArgs cmdArgs,
            Config config,
            out Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap)
        {
            var snapshotsDictionary =
                new Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>>();

            var alreadyReadSnapshots = new Dictionary<string, List<Snapshot>>();

            //stores the equal result for every watch expression
            equalMap =
                new Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)>();

            foreach (var watchExpression in allWatchExpressions)
            {
                EvaulateSingleWatchExpression(watchExpression, cmdArgs, config,
                    snapshotsDictionary, alreadyReadSnapshots, equalMap);
            }

            if (cmdArgs.Init || cmdArgs.Update)
            {
                // ReSharper disable once PossibleInvalidOperationException
                if (config.CombineSnapshotFiles.Value)
                {
                    //we haven't written any snapshots yet...
                    BulkWriteCapturedSnapshots(config, snapshotsDictionary);
                }
                //else already created in EvaulateSingleWatchExpression
            }
        }

        private static void BulkWriteCapturedSnapshots(Config config,
            Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>> snapshotsDictionary)
        {
            //but now we know all watch expressions to combine for every file

            //we would write them one after another in the target file... then then wrap them by []
            //this should be equal to the json transform

            foreach (var pair in snapshotsDictionary)
            {
                //pair.Value.Count > 0 is checked in SaveSnapshots with message
                bool created =
                    SnapshotWrapperHelper.SaveSnapshots(pair.Value.Select(p => p.snapshot).ToList(),
                        config.WatchCodeDirName, config.SnapshotDirName);

                if (pair.Value.Count > 0)
                {
                    (Snapshot snapshot, WatchExpression watchExpression) firstTuple = pair.Value.First();
                    Logger.Info(
                        $"snapshot for all watch expressions in for doc " +
                        $"file: {firstTuple.watchExpression.DocumentationFilePath} were created at: " +
                        $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), firstTuple.watchExpression, true)}");
                }
            }
        }


        private static void EvaulateSingleWatchExpression(WatchExpression watchExpression, CmdArgs cmdArgs,
            Config config,
            Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>> snapshotsDictionary,
            Dictionary<string, List<Snapshot>> alreadyReadSnapshots,
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap
        )
        {
            if (cmdArgs.Init)
            {
                #region --- init option

                // ReSharper disable once PossibleInvalidOperationException
                if (config.CombineSnapshotFiles.Value)
                {
                    //because we store combined snapshots we read all snapshots in the file
                    //actually we would only need until we found the right snapshot but we assume
                    //that we almost need to read all snapshots from the file anyway

                    //so store the already read snapshots

                    bool sineleSnapshotExists = false;

                    if (alreadyReadSnapshots.TryGetValue(watchExpression.WatchExpressionFilePath,
                        out var alreayReadSnapshots))
                    {
                        if (alreayReadSnapshots.Any(p => p.LineRange == watchExpression.LineRange))
                            sineleSnapshotExists = true;
                    }
                    else
                    {
                        sineleSnapshotExists =
                            SnapshotHelper.SnapshotCombinedExists(
                                DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName,
                                    config.SnapshotDirName),
                                watchExpression, out var readSnapshots);

                        if (sineleSnapshotExists)
                        {
                            alreadyReadSnapshots.Add(watchExpression.WatchExpressionFilePath, readSnapshots);
                        }
                    }

                    if (sineleSnapshotExists)
                    {
                        Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                    $"already exists at: " +
                                    $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), watchExpression, true)}" +
                                    $", skipping");
                    }
                    else
                    {
                        //do not save the snapshot ... maybe we get snapshots for the same file...
                        // ReSharper disable once PossibleInvalidOperationException
                        var newSnapshot =
                            SnapshotWrapperHelper.CreateSnapshot(watchExpression);

                        //inner func should have reported the error
                        if (newSnapshot == null) return;


                        if (snapshotsDictionary.TryGetValue(watchExpression.WatchExpressionFilePath,
                            out var snapshots))
                        {
                            snapshots.Add((newSnapshot, watchExpression));
                        }
                        else
                        {
                            snapshotsDictionary.Add(watchExpression.WatchExpressionFilePath,
                                new List<(Snapshot snapshot, WatchExpression watchExpression)>()
                                {
                                    (newSnapshot, watchExpression)
                                });
                        }
                        Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                    $"was added at: " +
                                    $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), watchExpression, true)}");
                    }

                    return;
                }

                //only create snapshots if they not exit yet
                //do not touch old snapshots
                bool snapshotExists =
                    SnapshotHelper.SnapshotExists(
                        DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName),
                        watchExpression);

                if (snapshotExists)
                {
                    //do not update, everything ok here

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"already exists at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), watchExpression, true)}" +
                                $", skipping");

                    return;
                }

                //create and save new snapshot
                // ReSharper disable once PossibleInvalidOperationException
                SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, config.WatchCodeDirName,
                    config.SnapshotDirName);

                Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                            $"was created at: " +
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), watchExpression, true)}");

                #endregion
            }

            else if (cmdArgs.Update)
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
                        snapshots.Add((newSnapshot, watchExpression));
                    }
                    else if (newSnapshot != null)
                    {
                        snapshotsDictionary.Add(watchExpression.WatchExpressionFilePath,
                            new List<(Snapshot snapshot, WatchExpression watchExpression)>()
                            {
                                (newSnapshot, watchExpression)
                            });
                    }
                    else // newSnapshot == null
                    {
                        //error already produced in CreateSnapshot
                        return;
                    }


                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"was added at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), watchExpression, true)}");

                    return;
                }

                //create and save new snapshot
                // ReSharper disable once PossibleInvalidOperationException
                SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, config.WatchCodeDirName,
                    config.SnapshotDirName);

                Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                            $"was saved at: " +
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName), watchExpression, true)}");

                #endregion
            }

            else if (cmdArgs.Compare)
            {
                #region --- compare option

                // ReSharper disable once PossibleInvalidOperationException
                string oldSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                    DynamicConfig.GetAbsoluteSnapShotDirPath(config.WatchCodeDirName, config.SnapshotDirName),
                    watchExpression, config.CombineSnapshotFiles.Value);

                Snapshot oldSnapshot;

                if (config.CombineSnapshotFiles.Value)
                {
                    //because we store combined snapshots we need to read every snapshot from the file anyway...
                    //so store them

                    if (alreadyReadSnapshots.TryGetValue(watchExpression.WatchExpressionFilePath,
                        out var alreayReadSnapshots))
                    {
                        oldSnapshot = alreayReadSnapshots.FirstOrDefault(p => p.LineRange == watchExpression.LineRange);
                    }
                    else
                    {
                        List<Snapshot> oldSnapshots = SnapshotWrapperHelper.ReadSnapshots(oldSnapshotPath);
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
        }


        private static void OutputEqualsResults(
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            out bool someSnapshotChanged, out bool someSnapshotUsedBottomToTopOrSearchLines, bool suppressOutput)
        {
            WatchExpression watchExpression;

            someSnapshotChanged = false;
            someSnapshotUsedBottomToTopOrSearchLines = false;

            foreach (var pair in equalMap)
            {
                var tuple = pair.Value;
                watchExpression = pair.Key;

                string range = watchExpression.LineRange == null
                    ? "somewhere"
                    : watchExpression.LineRange.ToString();

                if (tuple.wasEqual)
                {
                    if (tuple.newSnapshot.TriedBottomOffset)
                    {
                        someSnapshotUsedBottomToTopOrSearchLines = true;

                        Logger.Info(
                            $"snapshots are equal! file name: {watchExpression.WatchExpressionFilePath}, range from bottom: {tuple.newSnapshot.ReversedLineRange}");

                        if (suppressOutput == false)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write(watchExpression.WatchExpressionFilePath);
                            Console.ResetColor();
                            Console.Write(" lines inserted before, update line range --> ");
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.Write(watchExpression.GetDocumentationLocation());
                            Console.WriteLine();
                            Console.ResetColor();
                        }
                    }
                    else if (tuple.newSnapshot.TriedSearchFileOffset)
                    {
                        someSnapshotUsedBottomToTopOrSearchLines = true;

                        Logger.Info(
                            $"snapshots are equal! file name: {watchExpression.WatchExpressionFilePath}, found new range (via searching): {tuple.newSnapshot.ReversedLineRange}");

                        if (suppressOutput == false)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write(watchExpression.WatchExpressionFilePath);
                            Console.ResetColor();
                            Console.Write(" lines inserted before & after, update line range --> ");
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.Write(watchExpression.GetDocumentationLocation());
                            Console.WriteLine();
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Logger.Info(
                            $"snapshots are equal! file name: {watchExpression.WatchExpressionFilePath}, range: {range}");
                    }
                }
                else
                {
                    Logger.Info($"file: {watchExpression.WatchExpressionFilePath} has changed in range: {range}!");

                    someSnapshotChanged = true;

                    if (suppressOutput == false)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(watchExpression.WatchExpressionFilePath);

                        Console.ResetColor();
                        Console.Write(" changed, update --> ");

                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.Write(watchExpression.GetDocumentationLocation());
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                }
            }


            if (someSnapshotChanged == false && someSnapshotUsedBottomToTopOrSearchLines == false)
            {
                if (suppressOutput == false)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine("docs are ok");
                    Console.ResetColor();
                }
            }
        }


        private static void UpdateDocCommentWatchExpressions(
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            Config config)
        {
            //if not equal we cannot recover via searching from bottom (because file changed)
            var groupedWatchExpressions = equalMap
                    .GroupBy(p => p.Key.GetDocumentationLocation())
                    .ToList()
                ;

            foreach (var group in groupedWatchExpressions)
            {
                var tuples = group.ToList();

                //we need equal --> else we need manual update
                //if we used bottom or search the doc file must be updated (the line range)
                if (tuples.Any(p =>
                    p.Value.wasEqual &&
                    (p.Value.newSnapshot.TriedBottomOffset || p.Value.newSnapshot.TriedSearchFileOffset) ==
                    false))
                {
                    continue;
                }

                //here at least 1 in the same doc location (and watch expression position) changed
                //so update the whole expression (keep the not changed values but update where UsedBottomOffset)

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
                    config);
            }
        }

        static void PrintHelp()
        {
        }
    }
}
