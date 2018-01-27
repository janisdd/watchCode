using System.Collections.Generic;
using watchCode.model;

namespace watchCode.helpers
{
    public static class SnapshotWrapperHelper
    {
        public static bool prettyPrintSnapshots = true;

        public static Snapshot CreateSnapshot(WatchExpression watchExpression)
        {
            //create snapshot
            string path = DynamicConfig.GetAbsoluteFilePath(watchExpression.WatchExpressionFilePath);
            var snapshot =
                SnapshotHelper.CreateSnapshot(path, watchExpression);

            return snapshot;
        }


        public static Snapshot CreateSnapshotBasedOnOldSnapshot(WatchExpression watchExpression,
            Snapshot oldSnapshot, out bool snapshotsWereEqual)
        {
            //create snapshot
            string path = DynamicConfig.GetAbsoluteFilePath(watchExpression.WatchExpressionFilePath);

            if (watchExpression.LineRange == null)
            {
                //we need to read the whole file so no difference using stream indices...
                //or we don't check from bottom so CreateSnapshotBasedOnOldSnapshot would do the same as CreateSnapshot
                var newSnapshot = SnapshotHelper.CreateSnapshot(path, watchExpression);

                snapshotsWereEqual = AreSnapshotsEqual(oldSnapshot, newSnapshot);

                return newSnapshot;
            }


            var snapshot =
                SnapshotHelper.CreateSnapshotBasedOnOldSnapshot(path, watchExpression, oldSnapshot,
                    out snapshotsWereEqual);

            return snapshot;
        }


        public static bool AreSnapshotsEqual(Snapshot oldSnapshot, Snapshot newSnapshot, bool compareMetaData = false)
        {
            if (oldSnapshot == null || newSnapshot == null) return false;

            if (compareMetaData)
            {
                if (oldSnapshot.WatchExpressionFilePath != newSnapshot.WatchExpressionFilePath) return false;

                if (oldSnapshot.LineRange != newSnapshot.LineRange) return false;
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

        public static bool SaveSnapshot(Snapshot snapshot, WatchExpression watchExpression, Config config)
        {
            //save new snapshot
            string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotDirPath(config);
            return SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, watchExpression, prettyPrintSnapshots);
        }

        public static bool CreateAndSaveSnapshot(WatchExpression watchExpression, Config config, out Snapshot snapshot)
        {
            snapshot = CreateSnapshot(watchExpression);

            if (snapshot == null) return false;

            return SaveSnapshot(snapshot, watchExpression, config);
        }


        public static Snapshot ReadSnapshot(string absoluteSnapshotPath)
        {
            return SnapshotHelper.ReadSnapshot(absoluteSnapshotPath);
        }

        public static List<Snapshot> ReadSnapshots(string absoluteSnapshotPath)
        {
            return SnapshotHelper.ReadSnapshots(absoluteSnapshotPath);
        }


        public static bool SaveSnapshots(List<Snapshot> snapshots, Config config)
        {
            string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotDirPath(config);
            return SnapshotHelper.SaveSnapshots(snapshotDirPath, snapshots, prettyPrintSnapshots);
        }
    }
}
