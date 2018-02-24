using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using watchCode.helpers;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode
{
    public static class UpdateActionHandler
    {
         /// <summary>
        /// 
        /// </summary>
        /// <param name="watchExpressions"></param>
        /// <param name="config"></param>
        /// <returns>
        /// if snapshot == null -> error capturing snapshot
        /// if snapshot != null -> captured snapshot
        /// </returns>
        public static List<(WatchExpression watchExpression, Snapshot snapshot)> UpdateAction(List<WatchExpression> watchExpressions, Config config)
        {

            var capturedSnapshots = new List<(WatchExpression watchExpression, Snapshot snapshot)>();
            #region --- update option

            //update all snapshots ...
            //update old snapshots
            //create new snapshots

            foreach (var watchExpression in watchExpressions)
            {
                //create and save new snapshot
                // ReSharper disable once PossibleInvalidOperationException
                //create snapshot
                string path = DynamicConfig.GetAbsoluteSourceFilePath(watchExpression.SourceFilePath);
                var snapshot = SnapshotHelper.CreateSnapshot(path, watchExpression);

                string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotsDirPath(config);
                var created = SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, watchExpression);

                if (created)
                {
                    capturedSnapshots.Add((watchExpression, snapshot));

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"was saved at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotsDirPath(config), watchExpression)}");
                }
                else
                {
                    capturedSnapshots.Add((watchExpression, null));

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"was not saved");
                }

            }

           
            #endregion

            return capturedSnapshots;
        }

        
         /// <summary>
        /// 
        /// </summary>
        /// <param name="createdOrUpdateSnapshots">only the created snapshots, thus we also need the allWatchExpressions to know which we don't updated</param>
        public static void OutputUpdateResults(
            List<(WatchExpression watchExpression, Snapshot snapshot)> createdOrUpdateSnapshots)
        {
            Program.Stopwatch.Restart();
            Logger.Info("--- update snapshot results ---");
            Console.WriteLine($"{new string('-', Program.OkResultString.Length)} update results");

            foreach (var tuple in createdOrUpdateSnapshots)
            {
                if (tuple.snapshot == null)
                {
                    Console.BackgroundColor = Program.ChangedResultBgColor;
                    Console.Write(Program.ChangedResultString);
                    Console.ResetColor();

                    //maybe the file is deleted/no permission?
                    string path = DynamicConfig.GetAbsoluteSourceFilePath(tuple.watchExpression.SourceFilePath);
                    bool sourceFileExists = File.Exists(path);

                    string message =
                            sourceFileExists
                                ? $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot could not be updated, some error]"
                                : $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot could not be updated, source file does not exists or no permission]"
                        ;

                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else
                {
                    Console.BackgroundColor = Program.OkResultBgColor;
                    Console.Write(Program.OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot updated]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
            }

            Program.Stopwatch.Stop();
            Logger.Info($"--- end update snapshot results (took {StopWatchHelper.GetElapsedTime(Program.Stopwatch)}) ---");
        }
    }
}
