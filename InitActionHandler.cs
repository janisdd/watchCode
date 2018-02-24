using System;
using System.Collections.Generic;
using System.Diagnostics;
using watchCode.helpers;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode
{
    public static class InitActionHandler
    {        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="allWatchExpression"></param>
        /// <param name="config"></param>
        /// <returns>
        /// if snapshot == null && snapshotAlreadyExists -> we haven't captured snapshot because it already exists
        /// if snapshot == null && snapshotAlreadyExists == false -> error capturing snapshot
        /// if snapshot != null && snapshotAlreadyExists == true -> should not happen
        /// if snapshot != null && snapshotAlreadyExists == false -> captured snapshot
        /// </returns>
        public  static List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)>
            InitAction(List<WatchExpression> allWatchExpression, Config config)
        {
            var capturedSnapshots =
                new List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)>();

            #region --- init option

            foreach (var watchExpression in allWatchExpression)
            {
                //only create snapshots if they not exit yet
                //do not touch old snapshots
                bool snapshotExists =
                    SnapshotHelper.SnapshotExists(DynamicConfig.GetAbsoluteSnapShotsDirPath(config), watchExpression);

                if (snapshotExists)
                {
                    //do not update, everything ok here

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"already exists at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotsDirPath(config), watchExpression)}" +
                                $", skipping");

                    capturedSnapshots.Add((watchExpression, null, true));
                    return capturedSnapshots;
                }

                //create and save new snapshot

                //create snapshot
                string path = DynamicConfig.GetAbsoluteSourceFilePath(watchExpression.SourceFilePath);
                var snapshot = SnapshotHelper.CreateSnapshot(path, watchExpression);

                string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotsDirPath(config);
                var created = SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, watchExpression);

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
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotsDirPath(config), watchExpression)}");
            }



            #endregion

            return capturedSnapshots;
        }
        
        public  static void OutputInitResults(
            List<(WatchExpression watchExpression, Snapshot snapshot, bool snapshotAlreadyExists)> initedSnapshots)
        {
            Program.Stopwatch.Restart();
            Logger.Info("--- init results ---");
            Console.WriteLine($"{new string('-', Program.OkResultString.Length)} init results");

            foreach (var tuple in initedSnapshots)
            {
                if (tuple.snapshot == null && tuple.snapshotAlreadyExists)
                {
                    Console.BackgroundColor = Program.OkResultBgColor;
                    Console.Write(Program.OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot already exists]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else if (tuple.snapshot == null && tuple.snapshotAlreadyExists == false)
                {
                    Console.BackgroundColor = Program.ChangedResultBgColor;
                    Console.Write(Program.ChangedResultBgColor);
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
                    Console.BackgroundColor = Program.ChangedResultBgColor;
                    Console.Write(Program.ChangedResultBgColor);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [internal error occured!]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
                else //if (tuple.snapshot != null && tuple.snapshotAlreadyExists == false)
                {
                    Console.BackgroundColor = Program.OkResultBgColor;
                    Console.Write(Program.OkResultString);
                    Console.ResetColor();
                    var message =
                        $" {tuple.watchExpression.GetDocumentationLocation()} -> {tuple.watchExpression.GetSourceFileLocation()} [snapshot created]";
                    Console.WriteLine(message);

                    Logger.Info(message);
                }
            }

            Program.Stopwatch.Stop();
            Logger.Info($"--- end init results (took {StopWatchHelper.GetElapsedTime(Program.Stopwatch)}) ---");
        }
    }
}
