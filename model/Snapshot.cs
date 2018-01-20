using System.Collections.Generic;
using Newtonsoft.Json;

namespace watchCode.model
{
    public class Snapshot : ISnapshotLike
    {
        public string WatchExpressionFilePath { get; set; }
        public LineRange? LineRange { get; set; }

        public LineRange? ReversedLineRange { get; set; }


        /// <summary>
        /// the indices for the start and end of the specified lines (char position in stream)
        /// </summary>
        public IndexRange? LineRangeIndices { get; set; }

        /// <summary>
        /// the reverse line range from the bottom of the file up
        /// 
        /// LineRange does not change if lines unter the LineRange are added
        /// 
        /// ReverseLineRange does not change if lines above ReverseLineRange are added
        /// 
        /// in the original file LineRange and ReverseLineRange point to the same lines
        /// 
        /// if this is null this has no effect (take only LineRange then)
        /// </summary>
        public IndexRange? ReverseLineIndices { get; set; }

        /// <summary>
        /// the lines or just 1 line (a hash) if compressed
        /// 
        /// the lines are concat without new line char and the hash is calculated
        /// </summary>
        public List<string> Lines { get; set; }

        /// <summary>
        /// true: used <see cref="ReverseLineIndices"/> to get the lines
        /// </summary>
        [JsonIgnore]
        public bool UsedBottomOffset { get; set; }

        public Snapshot(string watchExpressionFilePath, LineRange? lineRange, LineRange? reversedLineRange,  IndexRange? lineRangeIndices,
            IndexRange? reverseLineIndices,
            List<string> lines)
        {
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = lineRange;
            ReversedLineRange = reversedLineRange;
            LineRangeIndices = lineRangeIndices;
            Lines = lines;
            ReverseLineIndices = reverseLineIndices;
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