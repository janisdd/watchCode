using System.Collections.Generic;

namespace watchCode.model
{
    public class Snapshot : ISnapshotLike
    {
        public string WatchExpressionFilePath { get; set; }
        public LineRange? LineRange { get; set; }
        public List<string> Lines { get; set; }

        public Snapshot(string watchExpressionFilePath, LineRange? lineRange, List<string> lines)
        {
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = lineRange;
            Lines = lines;
        }


        public string GetSnapshotFileNameWithoutExtension(bool combinedSnapshotFiles)
        {
            if (combinedSnapshotFiles) return WatchExpressionFilePath;
            
            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            return WatchExpressionFilePath + "_" + LineRange.Value.Start + "-" + LineRange.Value.End;
        }

        public override string ToString()
        {
            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            return WatchExpressionFilePath + ", " + LineRange.Value.Start + "-" + LineRange.Value.End;
        }
    }
}