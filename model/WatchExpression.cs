﻿namespace watchCode.model
{
    public struct WatchExpression : ISnapshotLike
    {
        /// <summary>
        /// the file path found in the watch expression
        /// </summary>
        public string WatchExpressionFilePath { get; set; }

        /// <summary>
        /// if this is null then we watch the whole file
        /// </summary>
        public LineRange? LineRange { get; set; }

        /// <summary>
        /// the file path (documentation file) where we found the watch expression
        /// relative the the root directory (so basically the same relative to as the watch expressions)
        /// </summary>
        public string DocumentationFilePath { get; set; }

        /// <summary>
        /// the lines where the watch expression was found
        /// </summary>
        public LineRange DocumentationLineRange { get; set; }
        

        public WatchExpression(string watchExpressionFilePath, LineRange? lineRange, string documentationFilePath, LineRange documentationLineRange)
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
            if (combinedSnapshotFiles)  return WatchExpressionFilePath;
            
            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            
            return WatchExpressionFilePath + "_" + LineRange.Value.Start + "-" + LineRange.Value.End;
        }
        
        public string GetFullIdentifier()
        {
            if (LineRange == null)
            {
                return WatchExpressionFilePath;
            }
            return WatchExpressionFilePath + "_" + LineRange.Value.Start + "-" + LineRange.Value.End;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="y"></param>
        /// <returns>true: the other y is fully included in this watch expression,
        /// false: not</returns>
        public bool IncludesOther(WatchExpression y)
        {
            //it's the same file
            if (WatchExpressionFilePath != y.WatchExpressionFilePath) return false;

            //if one matches the whole file then the other is not important
            if (LineRange == null || y.LineRange == null) return true;

            //if a rang includes the ther --> equal


            //range y is included in x range
            if (LineRange.Value.Start <= y.LineRange.Value.Start &&
                LineRange.Value.End >= y.LineRange.Value.End)
            {
                return true;
            }

            //range x is included in y range
            if (y.LineRange.Value.Start <= LineRange.Value.Start &&
                y.LineRange.Value.End >= LineRange.Value.End)
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
            return WatchExpressionFilePath + ", " + LineRange.Value.Start + "-" + LineRange.Value.End;
        }

        public  string GetDocumentationLocation()
        {
            return DocumentationFilePath + ", " + DocumentationLineRange.Start + "-" + DocumentationLineRange.End;
        }
    }
}