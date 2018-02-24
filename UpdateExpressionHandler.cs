using System;
using System.Collections.Generic;
using watchCode.helpers;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode
{
    /// <summary>
    /// when the lines changed but no auto migration is possible
    /// e.g. we had line 1-2 and now added a line in between we want --> 1-3
    /// 
    /// </summary>
    public static class UpdateExpressionHandler
    {
        public static
            List<(WatchExpression oldWatchExpression, WatchExpression newWatchExpression, Snapshot newSnapshot)>
            UpdateExpressions(
                List<(WatchExpression oldWatchExpression, WatchExpression newWatchExpression)> watchExpressions,
                Config config
            )
        {
            var result =
                new List<(WatchExpression oldWatchExpression, WatchExpression newWatchExpression, Snapshot newSnapshot)>
                    ();

           

            #region --- update expression option
            
            //all watchExpressions have the same source file & range

            foreach (var tuple in watchExpressions)
            {
                // ReSharper disable once PossibleInvalidOperationException
                string oldSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                    DynamicConfig.GetAbsoluteSnapShotsDirPath(config),
                    tuple.oldWatchExpression);


                string newSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                    DynamicConfig.GetAbsoluteSnapShotsDirPath(config),
                    tuple.newWatchExpression);

                bool snapshotAlreadyExists = IoHelper.CheckFileExists(newSnapshotPath, false);

                Snapshot snapshot = null;
                var equalMap =
                    new List<(WatchExpression watchExpression, bool wasEqual, bool needToUpdateWatchExpression, Snapshot
                        snapshot)>();

                if (snapshotAlreadyExists)
                {
                    //we are done we have already a watch expression watching the new file & line range

                    snapshot = SnapshotHelper.ReadSnapshot(newSnapshotPath);

                    if (snapshot == null)
                    {
                        result.Add((tuple.oldWatchExpression, tuple.newWatchExpression, null));
                        continue;
                    }

                    result.Add((tuple.oldWatchExpression, tuple.newWatchExpression, snapshot));
                    
                    equalMap.Add((tuple.newWatchExpression, true, true, snapshot));
                    
                    DocsHelper.UpdateWatchExpressionInDocFile(equalMap,
                        DynamicConfig.KnownFileExtensionsWithoutExtension, DynamicConfig.InitWatchExpressionKeywords,
                        config, out var r);
                }
                else
                {
                    string path = DynamicConfig.GetAbsoluteSourceFilePath(tuple.newWatchExpression.SourceFilePath);

                    //oldSnapshot = SnapshotHelper.ReadSnapshot(oldSnapshotPath);

                    //TODO opt if only path changed ... ok move, if line numbers changed create new snapshot

                    //if we move we could invalidate an another watch expression watching the same source file...
                    //bool moved = SnapshotHelper.MoveSnapshot(oldSnapshotPath, newSnapshotPath);

                    snapshot = SnapshotHelper.CreateSnapshot(path, tuple.newWatchExpression);

                    if (snapshot == null)
                    {
                        result.Add((tuple.oldWatchExpression, tuple.newWatchExpression, null));
                        continue;
                    }

                    string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotsDirPath(config);

                    bool created = SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, tuple.newWatchExpression);

                    result.Add((tuple.oldWatchExpression, tuple.newWatchExpression,
                            created
                                ? snapshot
                                : null
                        ));
                    
                    equalMap.Add((tuple.newWatchExpression, true, true, snapshot));
                    
                    DocsHelper.UpdateWatchExpressionInDocFile(equalMap,
                        DynamicConfig.KnownFileExtensionsWithoutExtension, DynamicConfig.InitWatchExpressionKeywords,
                        config, out var r);
                }

            }
            
            

            #endregion

            return result;
        }

        public static void OutputUpdateExpressionResults(
            List<(WatchExpression oldWatchExpression, WatchExpression newWatchExpression, Snapshot newSnapshot)>
                updateExpressions)
        {
            Program.Stopwatch.Restart();
            Logger.Info("--- update expression results ---");
            Console.WriteLine($"{new string('-', Program.OkResultString.Length)} update expression results");

            foreach (var tuple in updateExpressions)
            {
                if (tuple.newSnapshot == null)
                {
                    Console.BackgroundColor = Program.ChangedResultBgColor;
                    Console.Write(Program.ChangedResultBgColor);
                    Console.ResetColor();
                    var message =
                        $" {tuple.oldWatchExpression.GetDocumentationLocation()} -> {null} [could not create new snapshot!]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else
                {
                    Console.BackgroundColor = Program.OkResultBgColor;
                    Console.Write(Program.OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.oldWatchExpression.GetDocumentationLocation()} -> {tuple.newWatchExpression.GetSourceFileLocation()} (old: {tuple.oldWatchExpression.GetSourceFileLocation()}) [expression & snapshot updated]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
            }

            Program.Stopwatch.Stop();
            Logger.Info(
                $"--- end update expression results (took {StopWatchHelper.GetElapsedTime(Program.Stopwatch)}) ---");
        }
    }
}
