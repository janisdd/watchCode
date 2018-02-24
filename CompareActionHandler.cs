using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DiffMatchPatch;
using watchCode.helpers;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode
{
    public static class CompareActionHandler
    {
     
                /// <summary>
        /// 
        /// </summary>
        /// <param name="watchExpressions"></param>
        /// <param name="config"></param>
        public static Dictionary<WatchExpression, (bool wasEqual, bool needToUpdateRange, Snapshot oldSnapshot, Snapshot
            newSnapshot)> CompareAction(List<WatchExpression> watchExpressions, Config config
             )
        {

            var equalMap =
                new Dictionary<WatchExpression, (bool wasEqual, bool needToUpdateRange, Snapshot oldSnapshot, Snapshot
                    newSnapshot)>();
            
            #region --- compare option


            foreach (var watchExpression in watchExpressions)
            {
                // ReSharper disable once PossibleInvalidOperationException
                string oldSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                    DynamicConfig.GetAbsoluteSnapShotsDirPath(config),
                    watchExpression);

                Snapshot oldSnapshot;

                oldSnapshot = SnapshotHelper.ReadSnapshot(oldSnapshotPath);

                bool areEqual;

                Snapshot newSnapshot = null;


                if (oldSnapshot == null)
                {
                    //normally this can't happen ... because we created the snapshot...
                    //but on error or if someone deleted the snapshot in the snapshot dir...
                    equalMap.Add(watchExpression, (false, false, null, null));
                    continue;
                }


                //the old snapshot exists & has the same line ranges...

                string path = DynamicConfig.GetAbsoluteSourceFilePath(watchExpression.SourceFilePath);

                newSnapshot =
                    SnapshotHelper.CreateSnapshotBasedOnOldSnapshot(path, watchExpression, oldSnapshot,
                        out var snapshotsWereEqual, out bool needToUpdateRange);

                areEqual = snapshotsWereEqual;

                if (newSnapshot == null)
                {
                    equalMap.Add(watchExpression, (false, needToUpdateRange, oldSnapshot, null));
                }
                else
                {
                    //already checked in CreateSnapshotBasedOnOldSnapshot
                    equalMap.Add(watchExpression,
                        (areEqual, needToUpdateRange, oldSnapshot, newSnapshot));
                }
            }
            
            #endregion

            return equalMap;
        }

        
        public  static void OutputCompareResults(
            Dictionary<WatchExpression, (bool wasEqual, bool needToUpdateRange, Snapshot oldSnapshot, Snapshot
                newSnapshot)> equalMap,
            out bool someSnapshotChanged, out bool someSnapshotUsedBottomToTopOrSearchLines, bool suppressOutput)
        {
            Program.Stopwatch.Restart();
            Logger.Info("--- compare results ---");
            Console.WriteLine($"{new string('-', Program.OkResultString.Length)} compare results");

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
                    if (tuple.needToUpdateRange)
                    {
                        someSnapshotUsedBottomToTopOrSearchLines = true;

                        //user inserted lines before and after the watched lines --> line range is totaly wrong now

                        if (suppressOutput == false)
                        {
                            Console.BackgroundColor = Program.WarningResultBgColor;
                            Console.Write(Program.WarningResultString);
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
                        Console.BackgroundColor = Program.OkResultBgColor;
                        Console.Write(Program.OkResultString);
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
                        Console.BackgroundColor = Program.ChangedResultBgColor;
                        Console.Write(Program.ChangedResultString);
                        Console.ResetColor();

                        string path = DynamicConfig.GetAbsoluteSourceFilePath(watchExpression.SourceFilePath);
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


                                Console.WriteLine();
                                Console.WriteLine("old lines:");
                                tuple.oldSnapshot.Lines.ForEach(Console.WriteLine);

                                DiffMatchPatch.diff_match_patch diffAlgo = new diff_match_patch();

                                Console.WriteLine();
                                Console.WriteLine();
                                Console.WriteLine("new lines:");

                                tuple.newSnapshot.Lines.ForEach(Console.WriteLine);

                                Console.WriteLine();
                                Console.WriteLine();

                                Console.WriteLine("diff line by line:");
                                int max = Math.Max(tuple.oldSnapshot.Lines.Count, tuple.newSnapshot.Lines.Count);

                                for (int i = 0; i < max; i++)
                                {
                                    string oldLine = "";
                                    string newLine = "";

                                    if (i < tuple.oldSnapshot.Lines.Count) oldLine = tuple.oldSnapshot.Lines[i];
                                    if (i < tuple.newSnapshot.Lines.Count) newLine = tuple.newSnapshot.Lines[i];

                                    var diffs = diffAlgo.diff_main(oldLine, newLine);

                                    foreach (var diff in diffs)
                                    {
                                        if (diff.operation == Operation.EQUAL)
                                            Console.ForegroundColor = Logger.EqualColor;

                                        if (diff.operation == Operation.INSERT)
                                            Console.ForegroundColor = Logger.InsertedColor;

                                        if (diff.operation == Operation.DELETE)
                                            Console.ForegroundColor = Logger.DeletedColor;

                                        Console.Write(diff.text);
                                        Console.ResetColor();
                                    }

                                    Console.WriteLine();
                                }

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
                    Console.ForegroundColor = Program.OkResultBgColor;
                    var message = "all doc files are up to date!";
                    Console.WriteLine(message);
                    Logger.Info(message);
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = Program.ChangedResultBgColor;
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

            Program.Stopwatch.Stop();
            Logger.Info($"--- end compare results (took {StopWatchHelper.GetElapsedTime(Program.Stopwatch)}) ---");
        }

    }
}
