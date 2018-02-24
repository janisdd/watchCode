using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using watchCode.helpers;
using watchCode.model;

namespace watchCode
{
    public static class ClearUnusedSpansotsActionHandler
    {
        public static List<string> ClearUnusedSpansots(List<WatchExpression> allWatchExpressions, Config config)
        {
            var distincted = allWatchExpressions
                    .DistinctBy(p => p.GetSourceFileLocation())
                    .ToList()
                ;

            var snapShotDirPath = DynamicConfig.GetAbsoluteSnapShotsDirPath(config);

            var neededSnapshotPaths = distincted
                .Select(
                    p => SnapshotHelper.GetAbsoluteSnapshotFilePath(snapShotDirPath, p)
                );


            var snapshotPathsInDir = Directory.GetFiles(snapShotDirPath);

            HashSet<string> allSnapshots = new HashSet<string>(snapshotPathsInDir);
            HashSet<string> needeSnapshotSet = new HashSet<string>(neededSnapshotPaths);


            allSnapshots.ExceptWith(needeSnapshotSet);

            foreach (var notNeededSnapshotPath in allSnapshots)
            {
                File.Delete(notNeededSnapshotPath);
            }


            return allSnapshots.Select(p => IoHelper.GetRelativePath(p, snapShotDirPath)).ToList();
        }

        public static void OutputClearResults(List<string> clearedSnapshotsIdentifier)
        {
            Program.Stopwatch.Restart();
            Logger.Info("--- clear unused snapshots results ---");
            Console.WriteLine($"{new string('-', Program.OkResultString.Length)} clear unused snapshots results");

            foreach (var ident in clearedSnapshotsIdentifier)
            {
                Console.BackgroundColor = Program.OkResultBgColor;
                Console.Write(Program.OkResultString);
                Console.ResetColor();
                var message =
                    $" {ident} -> not used [deleted]";
                Console.WriteLine(message);

                Logger.Info(message);
            }

            Program.Stopwatch.Stop();
            Logger.Info(
                $"--- end clear unused snapshots results (took {StopWatchHelper.GetElapsedTime(Program.Stopwatch)}) ---");
        }
    }
}
