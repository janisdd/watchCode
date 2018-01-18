using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using watchCode.helpers;
using watchCode.model;

namespace watchCode
{
    class Program
    {
        public const int OkReturnCode = 0;
        public const int NotEqualCompareReturnCode = 1;
        public const int ErrorReturnCode = 2;

        static int Main(string[] args)
        {
            DynamicConfig.AbsoluteRootDirPath = Directory.GetCurrentDirectory();
            var cmdArgs = CmdArgsHelper.ParseArgs(args);


//            cmdArgs.Init = true;
            cmdArgs.Compare = true;


            //some checking 
            BootstrapHelper.Bootstrap(cmdArgs);


            #region get all doc files that could contain watch expressions 

            var allDocFileInfos = FileIteratorHelper.GetAllFiles(cmdArgs.Files, cmdArgs.Dirs,
                DynamicConfig.KnownFileExtensionsWithoutExtension.Keys.ToList(), true);

            #endregion

            #region get all watch expressions

            List<WatchExpression> allWatchExpressions = new List<WatchExpression>();

            foreach (var docFileInfo in allDocFileInfos)
            {
                var watchExpressions = WatchExpressionParseHelper.GetAllWatchExpressions(docFileInfo,
                    DynamicConfig.KnownFileExtensionsWithoutExtension,
                    DynamicConfig.initWatchExpressionKeywords);

                allWatchExpressions.AddRange(watchExpressions);
            }

            #endregion

            #region remove duplicates...

            var distinctWatchExpressions = allWatchExpressions
                .DistinctBy(p => p.GetIdentifier())
                .ToList();

            if (allWatchExpressions.Count != distinctWatchExpressions.Count)
            {
                Logger.Info(
                    $"ignoring {allWatchExpressions.Count - distinctWatchExpressions.Count} duplicate watch expressions");
            }

            #endregion


            //--- create dump expressiion file (if needed) 

            if (cmdArgs.CreateWatchExpressionsDumpFile)
            {
                DumpWatchExpressionHelper.DumpWatchExpressions(Path.Combine(DynamicConfig.AbsoluteRootDirPath,
                        cmdArgs.WatchCodeDir, cmdArgs.DumpWatchExpressionsFileName),
                    distinctWatchExpressions, true);
            }


            //--- main loop
            bool someSnapshotsWereNotEqual = false;

            foreach (var watchExpression in distinctWatchExpressions)
            {
                if (cmdArgs.Init)
                {
                    //only create snapshots if they not exit yet
                    //do not touch old snapshots
                    bool snapshotExists =
                        SnapshotHelper.SnapshotExists(DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs),
                            watchExpression);

                    if (snapshotExists)
                    {
                        //do not update, everything ok here
                        continue;
                    }

                    //create and save new snapshot
                    SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, cmdArgs);
                }

                if (cmdArgs.Update)
                {
                    //update all snapshots ...
                    //update old snapshots
                    //create new snapshots

                    //create and save new snapshot
                    SnapshotWrapperHelper.CreateAndSaveSnapshot(watchExpression, cmdArgs);
                }


                if (cmdArgs.Compare)
                {
                    string oldSnapshotPath = SnapshotHelper.GetAbsoluteSnapshotFilePath(
                        DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs), watchExpression);

                    //compare old and new snapshot...
                    var newSnapshot = SnapshotWrapperHelper.CreateSnapshot(watchExpression);
                    var oldSnapshot = SnapshotWrapperHelper.ReadSnapshot(oldSnapshotPath);


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
                }
            }


            if (cmdArgs.Compare && someSnapshotsWereNotEqual) return NotEqualCompareReturnCode;

            return OkReturnCode;
        }


        static void PrintHelp()
        {
        }
    }
}