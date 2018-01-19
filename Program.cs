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
            var cmdArgs = CmdArgsHelper.ParseArgs(args);

           
            //cmdArgs.Init = true;
            cmdArgs.Update = true;
            //cmdArgs.Compare = true;
            //TODO jsut for testing
            cmdArgs.ReduceWatchExpressions = false;


            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs);


            #region --- get all doc files that could contain watch expressions 

            var allDocFileInfos = FileIteratorHelper.GetAllFiles(cmdArgs.Files, cmdArgs.Dirs,
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

            if (cmdArgs.CreateWatchExpressionsDumpFile)
            {
                DumpWatchExpressionHelper.DumpWatchExpressions(Path.Combine(DynamicConfig.AbsoluteRootDirPath,
                        cmdArgs.WatchCodeDirName, cmdArgs.DumpWatchExpressionsFileName),
                    allWatchExpressions, true);
            }

            #endregion


            //--- main part

            bool someSnapshotsWereNotEqual = false;

            NoReduceWatchExpressionsRun(allWatchExpressions, cmdArgs, ref someSnapshotsWereNotEqual);


            if (cmdArgs.Compare && someSnapshotsWereNotEqual) return NotEqualCompareReturnCode;

            return OkReturnCode;
        }

        private static void NoReduceWatchExpressionsRun(List<WatchExpression> allWatchExpressions, CmdArgs cmdArgs,
            ref bool someSnapshotsWereNotEqual)
        {
            var snapshotsDictionary =
                new Dictionary<string, List<(Snapshot snapshot, WatchExpression watchExpression)>>();

            var alreadyReadSnapshots = new Dictionary<string, List<Snapshot>>();

            foreach (var watchExpression in allWatchExpressions)
            {
                if (cmdArgs.Init)
                {
                    #region --- init option

                    if (cmdArgs.CombineSnapshotFiles)
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
                                SnapshotHelper.SnapshotCombinedExists(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs),
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
                                        $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression, true)}" +
                                        $", skipping");
                        }
                        else
                        {
                            //do not save the snapshot ... maybe we get snapshots for the same file...
                            var newSnapshot =
                                SnapshotWrapperHelper.CreateSnapshot(watchExpression, !cmdArgs.CompressLines);

                            //inner func should have reported the error
                            if (newSnapshot == null) continue;


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
                                        $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression, true)}");
                        }

                        continue;
                    }

                    //only create snapshots if they not exit yet
                    //do not touch old snapshots
                    bool snapshotExists =
                        SnapshotHelper.SnapshotExists(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs),
                            watchExpression);

                    if (snapshotExists)
                    {
                        //do not update, everything ok here

                        Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                    $"already exists at: " +
                                    $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression, true)}" +
                                    $", skipping");

                        continue;
                    }

                    //create and save new snapshot
                    SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, cmdArgs, cmdArgs.CompressLines);

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"was created at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression, true)}");

                    #endregion
                }

                else if (cmdArgs.Update)
                {
                    #region --- update option

                    //update all snapshots ...
                    //update old snapshots
                    //create new snapshots

                    if (cmdArgs.CombineSnapshotFiles)
                    {
                        var newSnapshot = SnapshotWrapperHelper.CreateSnapshot(watchExpression, cmdArgs.CompressLines);

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
                                    $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression, true)}");

                        continue;
                    }

                    //create and save new snapshot
                    SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, cmdArgs, cmdArgs.CompressLines);

                    Logger.Info($"snapshot for doc file: {watchExpression.GetDocumentationLocation()} " +
                                $"was saved at: " +
                                $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression, true)}");

                    #endregion
                }

                else if (cmdArgs.Compare)
                {
                    #region --- compare option

                    string oldSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                        DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression,
                        cmdArgs.CombineSnapshotFiles);

                    //compare old and new snapshot...
                    var newSnapshot = SnapshotWrapperHelper.CreateSnapshot(watchExpression, cmdArgs.CompressLines);

                    Snapshot oldSnapshot = null;


                    if (cmdArgs.CombineSnapshotFiles)
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
                        someSnapshotsWereNotEqual = true;
                        Console.WriteLine(
                            $"file: {watchExpression.WatchExpressionFilePath} has changed in range: {range}!");
                    }

                    #endregion
                }
            }


            if (cmdArgs.CombineSnapshotFiles)
            {
                //could be because of init or update... don't matter here

                //we haven't written any snapshots yet...
                //but now we know all watch expressions to combine for every file

                //TODO maybe we could do this better if the snapshot list gets too big
                //we would write them one after another in the target file... then then wrap them by []
                //this should be equal to the json transform

                foreach (var pair in snapshotsDictionary)
                {
                    //pair.Value.Count > 0 is checked in SaveSnapshots with message
                    bool created =
                        SnapshotWrapperHelper.SaveSnapshots(pair.Value.Select(p => p.snapshot).ToList(), cmdArgs);

                    if (pair.Value.Count > 0)
                    {
                        (Snapshot snapshot, WatchExpression watchExpression) firstTuple = pair.Value.First();
                        Logger.Info(
                            $"snapshot for all watch expressions in for doc " +
                            $"file: {firstTuple.watchExpression.DocumentationFilePath} were created at: " +
                            $"{SnapshotHelper.GetAbsoluteSnapshotFilePath(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), firstTuple.watchExpression, true)}");
                    }
                }
            }
        }


        static void ReducedWatchExpressionsRun(List<WatchExpression> allWatchExpressions, CmdArgs cmdArgs,
            ref bool someSnapshotsWereNotEqual)
        {
            //TODO

            #region grouping, so that we eventually don't need to evaluate all watch expression

//            //group by file name --> because we will store all watch expression for one file in one file
//            var groupedWatchExpressionsByFileName = allWatchExpressions
//                .GroupBy(p => p.GetSnapshotFileNameWithoutExtension())
//                .ToList();
//
//            /*
//             * nevertheless we can reduce the amount of watch expression more...
//             *
//             * given group X of watch expressions
//             *     if a line range is included in another watch expressions line range
//             *     we only need to evaluate the larger range
//             * 
//             *     BUT we still need the groups because the user wants to know where
//             *     he need to update the documentation
//             *
//             *     so just iterate through the group and give the result of the largest
//             *     expressions for every member of the group
//             *
//             *
//             * this is good BUT what if the documentation changes and we e.g. remove a whole line watch expression?
//             * 
//             */
//
//            var uniqueWatchExpressions = new List<(WatchExpression uniqueExpression, List<WatchExpression>)>();
//
//            foreach (var group in groupedWatchExpressionsByFileName)
//            {
//                WatchExpression largestRangeWatchExpression = default(WatchExpression);
//
//
//                foreach (var watchExpression in group)
//                {
//                }
//            }

            #endregion
        }

        static void PrintHelp()
        {
        }
    }
}