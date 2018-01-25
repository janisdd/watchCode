using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using watchCode.helpers;

namespace watchCode.model
{
    public class Snapshot : ISnapshotLike
    {
        private LineRange _reversedLineRange;

        /// <summary>
        /// the watched file path (relative)
        /// </summary>
        public string WatchExpressionFilePath { get; set; }

        /// <summary>
        /// the line range to watch
        /// </summary>
        public LineRange LineRange { get; set; }

        /// <summary>
        /// we need to allow this for json deserializing
        /// </summary>
        public LineRange ReversedLineRange
        {
            get => LineRange == null ? null : _reversedLineRange;
            set => _reversedLineRange = value;
        }

        /// <summary>
        /// the lines or null if we only use hashes
        /// </summary>
        public List<string> Lines { get; set; }

        /// <summary>
        /// the lines are concat without new line char and the hash is calculated
        /// </summary>
        public string AllLinesHash { get; set; }

        /// <summary>
        /// the number of total lines in the file
        /// </summary>
        public int TotalLinesInFile { get; set; }


        //--- stats, not stored in snapshot e.g. used to know if we need to know a doc file ---

        /// <summary>
        /// true: used <see cref="ReversedLineRange"/> to get the lines
        /// only needed when re writing docs and for this we need to compare first which gives us this
        /// </summary>
        [JsonIgnore]
        public bool TriedBottomOffset { get; set; }

        /// <summary>
        /// true: we used search the whole file for the original files and found them
        /// </summary>
        [JsonIgnore]
        public bool TriedSearchFileOffset { get; set; }

        [Obsolete("do not use, only here because of json deserialization")]
        public Snapshot()
        {
        }

        public Snapshot(string watchExpressionFilePath,
            LineRange lineRange, LineRange reversedLineRange,
            int totalLinesInFile, List<string> lines)
        {
            
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = lineRange;
            ReversedLineRange = reversedLineRange;
            Lines = lines;

            TotalLinesInFile = totalLinesInFile;
            StringBuilder b = new StringBuilder();
            foreach (var line in Lines)
            {
                b.Append(line);
            }
            AllLinesHash = HashHelper.GetHash(b.ToString());
        }

        public Snapshot(string watchExpressionFilePath, string fileHash)
        {
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = null;
            ReversedLineRange = null;
            Lines = null;
            TotalLinesInFile = 0;

            AllLinesHash = fileHash;

        }


        public string GetSnapshotFileNameWithoutExtension(bool combinedSnapshotFiles)
        {
            if (combinedSnapshotFiles) return WatchExpressionFilePath;

            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            return WatchExpressionFilePath + "_" + LineRange.Start + "-" + LineRange.End;
        }

        public override string ToString()
        {
            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            return WatchExpressionFilePath + ", " + LineRange.Start + "-" + LineRange.End;
        }
    }
}