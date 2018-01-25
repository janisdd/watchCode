namespace watchCode.model
{
    public struct WatchExpression : ISnapshotLike
    {
        public string WatchExpressionFilePath { get; set; }
        public LineRange LineRange { get; set; }

        /// <summary>
        /// the file path (documentation file) where we found the watch expression
        /// relative the the root directory (so basically the same relative to as the watch expressions)
        /// </summary>
        public string DocumentationFilePath { get; set; }

        /// <summary>
        /// the lines where the watch expression was found
        /// </summary>
        public LineRange DocumentationLineRange { get; set; }


        public WatchExpression(string watchExpressionFilePath, LineRange lineRange, string documentationFilePath,
            LineRange documentationLineRange)
        {
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = lineRange;
            DocumentationFilePath = documentationFilePath;
            DocumentationLineRange = documentationLineRange;
        }

        /// <summary>
        /// used to determ the file name for the snapshot
        /// </summary>
        /// <returns></returns>
        public string GetSnapshotFileNameWithoutExtension(bool combinedSnapshotFiles)
        {
            if (combinedSnapshotFiles) return WatchExpressionFilePath;

            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }

            return WatchExpressionFilePath + "_" + LineRange.Start + "-" + LineRange.End;
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
            if (WatchExpressionFilePath != y.WatchExpressionFilePath) return false;

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

        public override string ToString()
        {
            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            return WatchExpressionFilePath + ", " + LineRange.Start + "-" + LineRange.End;
        }

        public string GetFullIdentifier()
        {
            if (LineRange == null)
            {
                return DocumentationFilePath + ", " + DocumentationLineRange.Start + "-" + DocumentationLineRange.End +
                       "_" + WatchExpressionFilePath;
            }
            return DocumentationFilePath + ", " + DocumentationLineRange.Start + "-" + DocumentationLineRange.End +
                   "_" + WatchExpressionFilePath + ", " + LineRange.Start + "-" + LineRange.End;
            ;
        }

        public string GetDocumentationLocation()
        {
            return DocumentationFilePath + ", " + DocumentationLineRange.Start + "-" + DocumentationLineRange.End;
        }
    }
}