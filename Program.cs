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
        public const int NeedToUpdateDocRanges = 2;
        public const int ErrorReturnCode = 100;

        static int Main(string[] args)
        {
            DynamicConfig.AbsoluteRootDirPath = Directory.GetCurrentDirectory();

            var (cmdArgs, config) = CmdArgsHelper.ParseArgs(args);

            return Run(cmdArgs, config);
        }


        public static int Run(CmdArgs cmdArgs, Config config)
        {
            int returnCode = OkReturnCode;
            //TODO
            //reduce and alsoUseReverseLines don't work togethere
            //because reducing will take one compare for the whole file and copy results
            //even if bottom offset would be ok

            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs, config);

            //cmdArgs.Init = true;
            //cmdArgs.Update = true;

            cmdArgs.Compare = true;

            //after updating docs all bottom to top snapshots are invalid (ranges)
            //after the docs update we don't know which snapshot belongs to which doc watch expression...

            //TODO maybe update the snapshots with the docs because the content of the snapshot won't change
            //only the line numbers...

            cmdArgs.CompareAndUpdateDocs = true;

            config.AlsoUseReverseLines = true;

            config.CompressLines = false;

            config.DirsToIgnore.Add(DynamicConfig.GetAbsoluteWatchCodeDirPath(config.WatchCodeDirName));
            
            if (Config.Validate(config, cmdArgs) == false)
            {
                Logger.Error("config was invalid, exitting");
                PrintHelp();
                return ErrorReturnCode;
            }

            

            #region --- get all doc files that could contain watch expressions 

            var allDocFileInfos = FileIteratorHelper.GetAllFiles(config.Files, config.Dirs,
                DynamicConfig.KnownFileExtensionsWithoutExtension.Keys.ToList(), true, config.FilesToIgnore,
                config.DirsToIgnore);

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
                OutputEqualsResults(equalMap, out var someSnapshotChanged, out var someSnapshotUsedBottomToTopLines);

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
                            SnapshotWrapperHelper.CreateSnapshot(watchExpression, !config.CompressLines.Value,
                                config.AlsoUseReverseLines);

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
                    config.SnapshotDirName, config.CompressLines.Value, config.AlsoUseReverseLines);

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
                        SnapshotWrapperHelper.CreateSnapshot(watchExpression, config.CompressLines.Value,
                            config.AlsoUseReverseLines);

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
                    config.SnapshotDirName, config.CompressLines.Value, config.AlsoUseReverseLines);

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
                                p.ReversedLineRange != null && DocsHelper.GetNewLineRange(watchExpression, p) ==
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
                    areEqual = false;
                    equalMap.Add(watchExpression, (areEqual, oldSnapshot, newSnapshot));

                    return;
                }
                else if (oldSnapshot.LineRange == null || oldSnapshot.ReversedLineRange == null)
                {
                    //old snapshot was for the whote file or we don't
                    //know reversed lines ... so take normal snapshot

                    // ReSharper disable once PossibleInvalidOperationException
                    newSnapshot =
                        SnapshotWrapperHelper.CreateSnapshot(watchExpression, config.CompressLines.Value,
                            config.AlsoUseReverseLines);
                }
                else
                {
                    //the old snapshot exists & has the same line ranges...

                    // ReSharper disable once PossibleInvalidOperationException
                    newSnapshot =
                        SnapshotWrapperHelper.CreateSnapshotBasedOnOldSnapshot(watchExpression,
                            config.CompressLines.Value, oldSnapshot, config.AlsoUseReverseLines);
                }


                if (newSnapshot == null)
                {
                    areEqual = false;
                    equalMap.Add(watchExpression, (areEqual, oldSnapshot, null));
                }
                else
                {
                    areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);
                    equalMap.Add(watchExpression,
                        (areEqual, oldSnapshot, newSnapshot));
                }

                #endregion
            }
        }


        private static void OutputEqualsResults(
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            out bool someSnapshotChanged, out bool someSnapshotUsedBottomToTopLines)
        {
            WatchExpression watchExpression;

            someSnapshotChanged = false;
            someSnapshotUsedBottomToTopLines = false;

            foreach (var pair in equalMap)
            {
                var tuple = pair.Value;
                watchExpression = pair.Key;

                string range = watchExpression.LineRange == null
                    ? "somewhere"
                    : watchExpression.LineRange.ToString();

                if (tuple.wasEqual)
                {
                    if (tuple.newSnapshot.UsedBottomOffset)
                    {
                        someSnapshotUsedBottomToTopLines = true;

                        Logger.Info(
                            $"snapshots are equal! file name: {watchExpression.WatchExpressionFilePath}, range from bottom: {tuple.newSnapshot.ReversedLineRange}");

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(watchExpression.WatchExpressionFilePath);
                        Console.ResetColor();
                        Console.Write(" lines inserted before, update line range --> ");
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.Write(watchExpression.GetDocumentationLocation());
                        Console.WriteLine();
                        Console.ResetColor();
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


            if (someSnapshotChanged == false && someSnapshotUsedBottomToTopLines == false)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine("docs are ok");
                Console.ResetColor();
            }
        }


        private static void UpdateDocCommentWatchExpressions(
            Dictionary<WatchExpression, (bool wasEqual, Snapshot oldSnapshot, Snapshot newSnapshot)> equalMap,
            Config config)
        {
            var groupedWatchExpressions = equalMap
                    .Where(p => p.Value.wasEqual) //if not equal we cannot recover via searching from bottom
                    .GroupBy(p => p.Key.GetDocumentationLocation())
                    .ToList()
                ;

            foreach (var group in groupedWatchExpressions)
            {
                var tuple = group.ToList();
                var expressionAndSnapshotTuples = tuple.Select(p => (p.Key, p.Value.newSnapshot)).ToList();

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