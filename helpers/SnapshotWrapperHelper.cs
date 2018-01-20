using System.Collections.Generic;
using watchCode.model;

namespace watchCode.helpers
{
    public static class SnapshotWrapperHelper
    {
        public static bool prettyPrintSnapshots = true;

        public static Snapshot CreateSnapshot(WatchExpression watchExpression, bool compressLines,
            bool alsoUseReverseLines)
        {
            //create snapshot
            string path = DynamicConfig.GetAbsoluteFileToWatchPath(watchExpression.WatchExpressionFilePath);
            var snapshot =
                SnapshotHelper.CreateSnapshot(path, watchExpression, compressLines, alsoUseReverseLines);

            return snapshot;
        }


        public static Snapshot CreateSnapshotBasedOnOldSnapshotWithIndices(WatchExpression watchExpression,
            bool compressLines,
            Snapshot oldSnapshot, bool alsoUseReverseLines)
        {
            //create snapshot
            string path = DynamicConfig.GetAbsoluteFileToWatchPath(watchExpression.WatchExpressionFilePath);

            if (watchExpression.LineRange == null)
            {
                //we need to read the whole file so no difference using stream indices...
                return SnapshotHelper.CreateSnapshot(path, watchExpression, compressLines, alsoUseReverseLines);
            }

            var snapshot =
                SnapshotHelper.CreateSnapshotBasedOnOldSnapshotWithIndices(path, watchExpression, compressLines,
                    oldSnapshot);

            return snapshot;
        }


        public static Snapshot CreateSnapshotBasedOnOldSnapshot(WatchExpression watchExpression, bool compressLines,
            Snapshot oldSnapshot, bool alsoUseReverseLines)
        {
            //create snapshot
            string path = DynamicConfig.GetAbsoluteFileToWatchPath(watchExpression.WatchExpressionFilePath);

            if (watchExpression.LineRange == null || alsoUseReverseLines == false)
            {
                //we need to read the whole file so no difference using stream indices...
                //or we don't check from bottom so CreateSnapshotBasedOnOldSnapshot would do the same as CreateSnapshot
                return SnapshotHelper.CreateSnapshot(path, watchExpression, compressLines, alsoUseReverseLines);
            }


            var snapshot =
                SnapshotHelper.CreateSnapshotBasedOnOldSnapshot(path, watchExpression, compressLines, oldSnapshot);

            return snapshot;
        }


        public static bool AreSnapshotsEqual(Snapshot oldSnapshot, Snapshot newSnapshot, bool compareMetaData = true)
        {
            if (oldSnapshot == null || newSnapshot == null) return false;

            if (compareMetaData)
            {
                if (oldSnapshot.WatchExpressionFilePath != newSnapshot.WatchExpressionFilePath) return false;

                if (oldSnapshot.LineRange.HasValue != newSnapshot.LineRange.HasValue) return false;


                if (oldSnapshot.LineRange.HasValue && newSnapshot.LineRange.HasValue)
                {
                    if (oldSnapshot.LineRange.Value != newSnapshot.LineRange.Value) return false;
                }
            }

            //this cannot happen if the watch expression not change
            //if the watch expression was updaten then the file name should have changed

            if (oldSnapshot.Lines.Count != newSnapshot.Lines.Count) return false; //check this for more common cases...


            for (int i = 0; i < oldSnapshot.Lines.Count; i++)
            {
                var oldLine = oldSnapshot.Lines[i];
                var newLine = newSnapshot.Lines[i];

                if (oldLine != newLine) return false;
            }

            return true;
        }


        //--- for single snapshots / no combine snapshots

        public static bool SaveSnapshot(Snapshot snapshot, string watchCodeDirName, string snapshotDirName)
        {
            //save new snapshot
            string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotDirPath(watchCodeDirName, snapshotDirName);
            return SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, prettyPrintSnapshots);
        }

        public static bool CreateAndSaveSnapshot(WatchExpression watchExpression, string watchCodeDirName,
            string snapshotDirName, bool compressLines, bool alsoUseReverseLines)
        {
            var snapShot = CreateSnapshot(watchExpression, compressLines, alsoUseReverseLines);

            return SaveSnapshot(snapShot, watchCodeDirName, snapshotDirName);
        }


        public static Snapshot ReadSnapshot(string absoluteSnapshotPath)
        {
            return SnapshotHelper.ReadSnapshot(absoluteSnapshotPath);
        }

        public static List<Snapshot> ReadSnapshots(string absoluteSnapshotPath)
        {
            return SnapshotHelper.ReadSnapshots(absoluteSnapshotPath);
        }


        public static bool SaveSnapshots(List<Snapshot> snapshots, string watchCodeDirName, string snapshotDirName)
        {
            string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotDirPath(watchCodeDirName, snapshotDirName);
            return SnapshotHelper.SaveSnapshots(snapshotDirPath, snapshots, prettyPrintSnapshots);
        }
    }
}