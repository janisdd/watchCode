using System;

namespace watchCode.model
{
    public class WatchExpression : ISnapshotLike
    {
        /// <summary>
        /// the source file path
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// the line range to watch
        /// or null to watch the whole file (for any changes)
        /// </summary>
        public LineRange LineRange { get; set; }

        /// <summary>
        /// the file path (documentation file) where we found the watch expression
        /// relative the the root directory (so basically the same relative to as the watch expressions)
        /// </summary>
        public string DocFilePath { get; set; }

        /// <summary>
        /// the lines where the watch expression was found
        /// </summary>
        public LineRange DocLineRange { get; set; }

        /// <summary>
        /// the used comment format (in case we need to rewrite)
        /// </summary>
        public CommentPattern UsedCommentFormat { get; set; }

        [Obsolete("do not use, only here because of json deserialization")]
        public WatchExpression()
        {
        }


        public WatchExpression(string sourceFilePath, LineRange lineRange, string docFilePath,
            LineRange docLineRange, CommentPattern usedCommentFormat)
        {
            SourceFilePath = sourceFilePath;
            LineRange = lineRange;
            DocFilePath = docFilePath;
            DocLineRange = docLineRange;
            UsedCommentFormat = usedCommentFormat;
        }

        /// <summary>
        /// used to determ the file name for the snapshot
        /// </summary>
        /// <returns></returns>
        public string GetSnapshotFileNameWithoutExtension()
        {
            return SourceFilePath + "_" + LineRange.Start + "-" + LineRange.End;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="y"></param>
        /// <returns>true: the other y is fully included in this watch expression (line range wise),
        /// false: not</returns>
        public bool IncludesOther(WatchExpression y)
        {
            //it's the same file
            if (SourceFilePath != y.SourceFilePath) return false;

            //if one matches the whole file then the other is not important
            if (LineRange == null) return true;

            //here this.LineRange is != null so y is always larger
            if (y.LineRange == null) return false;

            //if a rang includes the ther --> equal


            //range y is included in x range
            if (LineRange.Start <= y.LineRange.Start &&
                LineRange.End >= y.LineRange.End)
            {
                return true;
            }

            return false;
        }

        public string GetFullIdentifier()
        {
            return DocFilePath + ", " + DocLineRange.Start + "-" + DocLineRange.End +
                   "_" + SourceFilePath + ", " + LineRange.Start + "-" + LineRange.End;
            ;
        }

        public string GetDocumentationLocation()
        {
            return DocFilePath + ", " + DocLineRange.Start + "-" + DocLineRange.End;
        }


        public static bool operator ==(WatchExpression w1, WatchExpression w2)
        {
            if (ReferenceEquals(w1, w2))
            {
                return true;
            }

            if (ReferenceEquals(w1, null))
            {
                return false;
            }

            if (ReferenceEquals(w2, null))
            {
                return false;
            }

            return w1.DocFilePath == w2.DocFilePath &&
                   w1.DocLineRange == w2.DocLineRange &&
                   w1.SourceFilePath == w2.SourceFilePath &&
                   w1.LineRange == w2.LineRange;
        }

        public static bool operator !=(WatchExpression w1, WatchExpression w2)
        {
            return !(w1 == w2);
        }

        public string GetSourceFileLocation()
        {
            if (LineRange == null)
            {
                return SourceFilePath;
            }

            return SourceFilePath + ", " + LineRange.Start + "-" + LineRange.End;
        }
    }
}
