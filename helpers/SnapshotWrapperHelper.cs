using watchCode.model;

namespace watchCode.helpers
{
    public static class SnapshotWrapperHelper
    {
        public static Snapshot CreateSnapshot(WatchExpression watchExpression)
        {
            //create snapshot
            string path = DynamicConfig.GetAbsoluteFileToWatchPath(watchExpression.WatchExpressionFilePath);
            var snapshot =
                SnapshotHelper.CreateSnapshot(path, watchExpression.WatchExpressionFilePath, watchExpression.LineRange);

            return snapshot;
        }

        public static bool SaveSnapshot(Snapshot snapshot, CmdArgs cmdArgs)
        {
            //save new snapshot
            string snapshotDirPath = DynamicConfig.GetAbsoluteSnapShotDirPath(cmdArgs);
            return SnapshotHelper.SaveSnapshot(snapshotDirPath, snapshot, true);
        }

        public static bool CreateAndSaveSnapshot(WatchExpression watchExpression, CmdArgs cmdArgs)
        {
            var snapShot = CreateSnapshot(watchExpression);

            return SaveSnapshot(snapShot, cmdArgs);
        }


        public static Snapshot ReadSnapshot(string absoluteSnapshotPath)
        {
            return SnapshotHelper.ReadSnapshot(absoluteSnapshotPath);
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
    }
}