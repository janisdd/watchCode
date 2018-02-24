using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using watchCode.helpers;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode
{
    public static class UpdateDocsHandler
    {
        public static void UpdateDocCommentWatchExpressions(
            Dictionary<WatchExpression, (bool wasEqual, bool needToUpdateRange, Snapshot oldSnapshot, Snapshot
                newSnapshot)> equalMap,
            Config config)
        {
            Program.Stopwatch.Restart();
            Logger.Info("--- auto updating docs ---");
            Console.WriteLine($"{new string('-', Program.OkResultString.Length)} update doc files");


            var allLatestWatchExpressions =
                new List<(bool needToUpdateWatchExpression, WatchExpression latestWatchExpression,
                    Snapshot latestSnapshot)>();


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

            var list = equalMap.ToList();

            foreach (var tuple in list)
            {
                if (tuple.Value.wasEqual)
                {
                    if (tuple.Value.needToUpdateRange)
                    {
                        var expressionAndSnapshotTuples =
                            (tuple.Key, tuple.Value.wasEqual, tuple.Value.needToUpdateRange, tuple.Value.newSnapshot);
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
                        var updated = DocsHelper.UpdateWatchExpressionInDocFile(
                            new List<(WatchExpression watchExpression, bool wasEqual, bool needToUpdateWatchExpression,
                                Snapshot snapshot)>()
                            {
                                expressionAndSnapshotTuples
                            },
                            DynamicConfig.KnownFileExtensionsWithoutExtension,
                            DynamicConfig.InitWatchExpressionKeywords,
                            config, out var updateWatchExpressionsList);


                        Console.BackgroundColor = Program.OkResultBgColor;
                        Console.Write(Program.AutoUpdateResultString);
                        Console.ResetColor();

                        var message =
                            $" {tuple.Key.GetDocumentationLocation()} -> new: " +
                            $"{updateWatchExpressionsList.First().updateWatchExpression.GetSourceFileLocation()} (old: " +
                            $"{updateWatchExpressionsList.First().oldWatchExpression.GetSourceFileLocation()}) [auto updated]";
                        Console.WriteLine(message);

                        Logger.Info(message);

                        allLatestWatchExpressions.Add((true, updateWatchExpressionsList.First().updateWatchExpression,
                            tuple.Value.newSnapshot));
                    }
                    else
                    {
                        Console.BackgroundColor = Program.OkResultBgColor;
                        Console.Write(Program.OkResultString);
                        Console.ResetColor();

                        var message =
                            $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [not changed]";
                        Console.WriteLine(message);

                        Logger.Info(message);
                    }
                }
                else
                {
                    Console.BackgroundColor = Program.ChangedResultBgColor;
                    Console.Write(Program.ChangedResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.Key.GetDocumentationLocation()} -> {tuple.Key.GetSourceFileLocation()} [no auto update possible, some line content changed?]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
            }

            #endregion


            #region update snapshots where line range changed

            var localStopWatch = new Stopwatch();
            localStopWatch.Start();
            Logger.Info("----- auto updating snapshots (where the watch expression line range changed)");

            //we need to group by the source files (if we use combine snapshots)
            //because we would overwrite the snapshot files with only the updated expressions and this would
            //remove the not updated expressions for this source file

//            if (allLatestWatchExpressions.Count != equalMap.Count)
//            {
//                //just some check that we checked every expression
//                Logger.Error("we missed some watch expressions... internal error!");
//                throw new Exception("we missed some watch expressions... internal error!");
//            }

            //var newWatchExpressions = allUpdateWatchExpressionsList.Select(p => p.updatedWatchExpression).ToList();

            NoReducUpdateAfterUpdatingDocs(allLatestWatchExpressions, config,
                out var createdAndUpdatedSnapshots);

//                NoReduceWatchExpressionsRun(newWatchExpressions, MainAction.Update, config,
//                    out equalMap,
//                    out var initedSnapshots,
//                    out var createdAndUpdatedSnapshots);

            localStopWatch.Stop();
            Logger.Info(
                $"----- end auto updating snapshots (took {StopWatchHelper.GetElapsedTime(localStopWatch)})");

            UpdateActionHandler.OutputUpdateResults(createdAndUpdatedSnapshots);

            #endregion


            Program.Stopwatch.Stop();
            Logger.Info($"--- end updating docs (took {StopWatchHelper.GetElapsedTime(Program.Stopwatch)})---");
        }

        private static void NoReducUpdateAfterUpdatingDocs(
            List<(bool needToUpdateWatchExpression, WatchExpression latestWatchExpression, Snapshot latestSnapshot)>
                needToUpdateSnapshotFilesTuples, Config config,
            out List<(WatchExpression watchExpression, Snapshot snapshot)> createdAndUpdatedSnapshots
        )
        {
            var snapshotsDictionary =
                new Dictionary<string, List<(WatchExpression watchExpression, Snapshot snapshot)>>();

            //update includes init...
            createdAndUpdatedSnapshots = new List<(WatchExpression watchExpression, Snapshot snapshot)>();

            foreach (var tuple in needToUpdateSnapshotFilesTuples)
            {
                //skip if we already have a snapshot...

                // if needToUpdateWatchExpression is true then latestSnapshot == null 
                //        --> should not happen

                // if needToUpdateWatchExpression is true then latestSnapshot != null ignore given snapshot... 
                //     update watch expression and snapshot

                // if needToUpdateWatchExpression is false and latestSnapshot == null
                //        --> we cannot auto update (or error) so no need to update watch expression (and snapshot file)

                //if needToUpdateWatchExpression is false and latestSnapshot != null then do not update expression
                //    and use the given snapshot (no need to create a new one)

                if (tuple.needToUpdateWatchExpression)
                {
                    if (tuple.latestSnapshot == null)
                    {
                        //should not happen
                        Logger.Error("internal error, needToUpdateWatchExpression is true, latestSnapshot is null");
                        throw new Exception(
                            "internal error, needToUpdateWatchExpression is true, latestSnapshot is null");
                    }
                    else
                    {
                        //the watch expression in doc file was already updated but to snapshot need to be updated too
                        //we already got the new snapshot but need to write it to the combined file

                        _AddSnapshot(snapshotsDictionary, (tuple.latestWatchExpression, tuple.latestSnapshot));
                        createdAndUpdatedSnapshots.Add((tuple.latestWatchExpression, tuple.latestSnapshot));

                        string path =
                            DynamicConfig.GetAbsoluteSourceFilePath(tuple.latestWatchExpression.SourceFilePath);
                        var snapshot = SnapshotHelper.CreateSnapshot(path, tuple.latestWatchExpression);

                        string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotsDirPath(config);
                        var created =
                            SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, tuple.latestWatchExpression);

                        if (created)
                        {
                            Logger.Info(
                                $"snapshot for doc file: {tuple.latestWatchExpression.GetDocumentationLocation()} " +
                                $"was saved at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotsDirPath(config), tuple.latestWatchExpression)}");
                        }
                        else
                        {
                            Logger.Info(
                                $"snapshot for doc file: {tuple.latestWatchExpression.GetDocumentationLocation()} " +
                                $"was not saved");
                        }
                    }
                }
                else
                {
                    if (tuple.latestSnapshot == null) //no auto update possible
                    {
                        //write the last known snapshot down because we can't do better
//                        _AddSnapshot(snapshotsDictionary, (tuple.latestWatchExpression, tuple.latestSnapshot));
//                        createdAndUpdatedSnapshots.Add((tuple.latestWatchExpression, tuple.latestSnapshot));
                    }
                    else
                    {
                        //everything was fine, the latest snapshot is the right one

                        //if no auto update is possible just use the old snapshot we can't do better anyway...

                        _AddSnapshot(snapshotsDictionary, (tuple.latestWatchExpression, tuple.latestSnapshot));
                        //we just copied the old one --> no real update of snapshot
                        //createdAndUpdatedSnapshots.Add((tuple.latestWatchExpression, tuple.latestSnapshot));
                    }
                }
            }
        }

        private static void _AddSnapshot(
            Dictionary<string, List<(WatchExpression watchExpression, Snapshot snapshot)>> snapshotsDictionary,
            (WatchExpression watchExpression, Snapshot snapshot) snapshotTupleToAdd)
        {
            if (snapshotsDictionary.TryGetValue(snapshotTupleToAdd.watchExpression.SourceFilePath,
                out var snapshots))
            {
                //add the snapshot to the others for the same source file (to bulk write)
                snapshots.Add((snapshotTupleToAdd.watchExpression, snapshotTupleToAdd.snapshot));
            }
            else
            {
                snapshotsDictionary.Add(snapshotTupleToAdd.watchExpression.SourceFilePath,
                    new List<(WatchExpression watchExpression, Snapshot snapshot )>()
                    {
                        (snapshotTupleToAdd.watchExpression, snapshotTupleToAdd.snapshot)
                    });
            }
        }
    }
}
