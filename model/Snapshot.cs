using System.Collections.Generic;
using Newtonsoft.Json;

namespace watchCode.model
{
    public class Snapshot : ISnapshotLike
    {
        public string WatchExpressionFilePath { get; set; }
        public LineRange? LineRange { get; set; }

        /// <summary>
        /// use  <see cref="SetReverseLineRange"/> to set this,
        /// we need to allow this for json deserializing
        /// </summary>
        public LineRange? ReversedLineRange { get; set; }

        /// <summary>
        /// the lines or just 1 line (a hash) if compressed
        /// 
        /// the lines are concat without new line char and the hash is calculated
        /// </summary>
        public List<string> Lines { get; set; }

        /// <summary>
        /// true: used <see cref="ReversedLineRange"/> to get the lines
        /// only needed when re writing docs and for this we need to compare first which gives us this
        /// </summary>
        [JsonIgnore]
        public bool UsedBottomOffset { get; set; }

        /// <summary>
        /// use  <see cref="SetReverseLineRange"/> to set this,
        /// we need to allow this for json deserializing
        /// </summary>
        public int TotalLines { get; set; }

        public Snapshot(string watchExpressionFilePath, LineRange? lineRange,
            List<string> lines)
        {
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = lineRange;
            Lines = lines;
            TotalLines = -1;
        }

        public void SetReverseLineRange(LineRange reversedLineRange, int totalLines)
        {
            ReversedLineRange = reversedLineRange;
            TotalLines = totalLines;
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