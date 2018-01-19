using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using Newtonsoft.Json;
using watchCode.helpers;
using watchCode.model;

namespace watchCode
{
    public class Program
    {
        public const int OkReturnCode = 0;
        public const int NotEqualCompareReturnCode = 1;
        public const int ErrorReturnCode = 2;

        static int Main(string[] args)
        {
            DynamicConfig.AbsoluteRootDirPath = Directory.GetCurrentDirectory();

            var (cmdArgs, config) = CmdArgsHelper.ParseArgs(args);


            //cmdArgs.Init = true;
            //cmdArgs.Update = true;
            cmdArgs.Compare = true;

            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs, config);


            if (Config.Validate(config) == false)
            {
                Logger.Error("config was invalid, exitting");
                return ErrorReturnCode;
            }


            #region --- get all doc files that could contain watch expressions 

            var allDocFileInfos = FileIteratorHelper.GetAllFiles(config.Files, config.Dirs,
                DynamicConfig.KnownFileExtensionsWithoutExtension.Keys.ToList(), true);

            #endregion

            #region --- get all watch expressions

            List<WatchExpression> allWatchExpressions = new List<WatchExpression>();


            foreach (var docFileInfo in allDocFileInfos)
            {
                var watchExpressions = WatchExpressionParseHelper.GetAllWatchExpressions(docFileInfo,
                    DynamicConfig.KnownFileExtensionsWithoutExtension,
                    DynamicConfig.initWatchExpressionKeywords);

                allWatchExpressions.AddRange(watchExpressions);
            }

            #endregion


            if (allWatchExpressions.Count == 0)
            {
                Logger.Warn("no watch expressions found");
            }

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


            Dictionary<WatchExpression, bool> equalMap;

            if (config.ReduceWatchExpressions.Value)
            {
                ReducedWatchExpressionsRun(allWatchExpressions, cmdArgs, config, out equalMap);
            }
            else
            {
                NoReduceWatchExpressionsRun(allWatchExpressions, cmdArgs, config, out equalMap);
            }


            if (cmdArgs.Compare)
            {
                OutputEqualsResults(equalMap, out var someSnapshotChanged);

                if (someSnapshotChanged) return NotEqualCompareReturnCode;
            }

            return OkReturnCode;
        }


        private static void NoReduceWatchExpressionsRun(List<WatchExpression> allWatchExpressions, CmdArgs cmdArgs,
            Config config, out Dictionary<WatchExpression, bool> equalMap)
        {
            var snapshotsDictionary =
                new Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>>();

            var alreadyReadSnapshots = new Dictionary<string, List<Snapshot>>();

            //stores the equal result for every watch expression
            equalMap = new Dictionary<WatchExpression, bool>();

            foreach (var watchExpression in allWatchExpressions)
            {
                EvaulateSingleWatchExpression(watchExpression, cmdArgs, config,
                    snapshotsDictionary, alreadyReadSnapshots, equalMap);
            }

            // ReSharper disable once PossibleInvalidOperationException
            if (config.CombineSnapshotFiles.Value)
            {
                //we haven't written any snapshots yet...
                //but now we know all watch expressions to combine for every file

                //could be because of init or update... don't matter here


                //TODO maybe we could do this better if the snapshot list gets too big
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
        }


        private static void ReducedWatchExpressionsRun(List<WatchExpression> allWatchExpressions, CmdArgs cmdArgs,
            Config config, out Dictionary<WatchExpression, bool> equalMap)
        {
            var snapshotsDictionary =
                new Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>>();

            var alreadyReadSnapshots = new Dictionary<string, List<Snapshot>>();

            //stores the equal result for every watch expression
            equalMap = new Dictionary<WatchExpression, bool>();


            #region grouping, so that we eventually don't need to evaluate all watch expression

            //group by file name --> because we will store all watch expression for one file in one file
            // ReSharper disable once PossibleInvalidOperationException
            var groupedWatchExpressionsByFileName = allWatchExpressions
                .GroupBy(p => p.GetSnapshotFileNameWithoutExtension(config.CombineSnapshotFiles.Value))
                .ToList();

            /*
             * nevertheless we can reduce the amount of watch expression more...
             *
             * given group X of watch expressions
             *     if a line range is included in another watch expressions line range
             *     we only need to evaluate the larger range
             * 
             *     BUT we still need the groups because the user wants to know where
             *     he need to update the documentation
             *
             *     so just iterate through the group and give the result of the largest
             *     expressions for every member of the group
             *
             */

            foreach (var group in groupedWatchExpressionsByFileName)
            {
                var sameFileNameWatchExpressions = group.ToList();
                if (sameFileNameWatchExpressions.Count == 0)
                {
                    Logger.Log("found empty group of watch expressions ... should not happen, skipping");
                }

                //TODO maybe reduce intersecting watch expressions...
                //not 100% sure this is correct...

                //we can have multiple nested regions...
                //store for every region the largest and the containg expressions
                var ranks = new List<(WatchExpression largest, List<WatchExpression> containedExpressions)>();

                //e.g. watch same file lines 6, 7-10, full file 1-11
                //then we would get two buckets but both has same full file as largest

                //so better sort them first by range
                //if an expression y would go into bucket x then we know x.start <= y.start

                var orderedSameFileNameWatchExpressions = sameFileNameWatchExpressions
                        .OrderBy(p => p.LineRange?.Start ?? Int32.MinValue)
                        .ToList()
                    ;

                for (int i = 0; i < orderedSameFileNameWatchExpressions.Count; i++) //already checked i=0
                {
                    var watchExpression = orderedSameFileNameWatchExpressions[i];

                    //first needs to be added manually
                    if (ranks.Count == 0)
                    {
                        ranks.Add((watchExpression, new List<WatchExpression>()));
                        continue;
                    }

                    //check if the watch expression is already included in another
                    for (int j = 0; j < ranks.Count; j++)
                    {
                        var tuple = ranks[j];

                        if (tuple.largest.IncludesOther(watchExpression))
                        {
                            tuple.containedExpressions.Add(watchExpression);
                        }
                        else if (watchExpression.IncludesOther(tuple.largest))
                        {
                            //found new largest
                            tuple.containedExpressions.Add(tuple.largest);
                            tuple.largest = watchExpression;
                        }
                        else
                        {
                            //create new group becasue distinct or intersect with other groups...
                            ranks.Add((watchExpression, new List<WatchExpression>()));
                            break;
                        }
                    }
                }

                //we found the largest watch expression..

                //now evaulate all necessary

                foreach (var tuple in ranks)
                {
                    EvaulateSingleWatchExpression(tuple.largest, cmdArgs, config,
                        snapshotsDictionary, alreadyReadSnapshots, equalMap);

                    var snapshotsWereEqual = equalMap[tuple.largest];

                    //copy result to sub line ranges
                    foreach (var watchExpression in tuple.containedExpressions)
                        equalMap.Add(watchExpression, snapshotsWereEqual);
                }
            }

            #endregion
        }


        private static void EvaulateSingleWatchExpression(WatchExpression watchExpression, CmdArgs cmdArgs,
            Config config,
            Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>> snapshotsDictionary,
            Dictionary<string, List<Snapshot>> alreadyReadSnapshots,
            Dictionary<WatchExpression, bool> equalMap
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
                            SnapshotWrapperHelper.CreateSnapshot(watchExpression, !config.CompressLines.Value);

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
                    config.SnapshotDirName, config.CompressLines.Value);

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
                        SnapshotWrapperHelper.CreateSnapshot(watchExpression, config.CompressLines.Value);

                    if (newSnapshot != null &&
                        snapshotsDictionary.TryGetValue(watchExpression.WatchExpressionFilePath, out var snapshots))
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

                    return;
                }

                //create and save new snapshot
                // ReSharper disable once PossibleInvalidOperationException
                SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, config.WatchCodeDirName,
                    config.SnapshotDirName, config.CompressLines.Value);

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

                //compare old and new snapshot...
                // ReSharper disable once PossibleInvalidOperationException
                var newSnapshot = SnapshotWrapperHelper.CreateSnapshot(watchExpression, config.CompressLines.Value);

                Snapshot oldSnapshot;


                if (config.CombineSnapshotFiles.Value)
                {
                    //because we store combined snapshots we need to read every snapshot from the file anyway...
                    //so store them

                    if (alreadyReadSnapshots.TryGetValue(watchExpression.WatchExpressionFilePath,
                        out var alreayReadSnapshots))
                    {
                        oldSnapshot = alreayReadSnapshots.FirstOrDefault(p => p.LineRange == newSnapshot.LineRange);
                    }
                    else
                    {
                        List<Snapshot> oldSnapshots = SnapshotWrapperHelper.ReadSnapshots(oldSnapshotPath);
                        alreadyReadSnapshots.Add(watchExpression.WatchExpressionFilePath, oldSnapshots);


                        oldSnapshot = oldSnapshots?.FirstOrDefault(p => p.LineRange == newSnapshot.LineRange);
                    }
                }
                else
                {
                    oldSnapshot = SnapshotWrapperHelper.ReadSnapshot(oldSnapshotPath);
                }

                bool areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                equalMap.Add(watchExpression, areEqual);

                #endregion
            }
        }


        private static void OutputEqualsResults(Dictionary<WatchExpression, bool> equalMap,
            out bool someSnapshotChanged)
        {
            bool areEqual;
            WatchExpression watchExpression;

            someSnapshotChanged = false;

            foreach (var pair in equalMap)
            {
                areEqual = pair.Value;
                watchExpression = pair.Key;

                string range = watchExpression.LineRange == null
                    ? "somewhere"
                    : watchExpression.LineRange.ToString();

                if (areEqual)
                {
                    Logger.Info(
                        $"snapshots are equal! file name: {watchExpression.WatchExpressionFilePath}, range: {range}");
                }
                else
                {
                    Logger.Info($"file: {watchExpression.WatchExpressionFilePath} has changed in range: {range}!");

                    someSnapshotChanged = true;

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

            if (someSnapshotChanged == false)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("docs are ok");
                Console.ResetColor();
            }
        }

        static void PrintHelp()
        {
        }
    }
}