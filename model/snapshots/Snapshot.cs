using System;
using System.Collections.Generic;

namespace watchCode.model.snapshots
{
    public class Snapshot : ISnapshotLike
    {
        public string SourceFilePath { get; set; }
        public string GetSnapshotFileNameWithoutExtension(bool combinedSnapshotFiles)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// the line range to watch
        /// </summary>
        public LineRange LineRange { get; set; }

        /// <summary>
        /// the lines or null if we only use hashes
        /// </summary>
        public List<string> Lines { get; set; }

        
        [Obsolete("do not use, only here because of json deserialization")]
        public Snapshot()
        {
        }

        public Snapshot(string sourceFilePath, LineRange lineRange, List<string> lines)
        {
            SourceFilePath = sourceFilePath;
            LineRange = lineRange;
            Lines = lines;
        }

        public string GetSnapshotFileNameWithoutExtension()
        {
            return SourceFilePath + "_" + LineRange.Start + "-" + LineRange.End;
        }
    }
}
