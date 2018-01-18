namespace watchCode.model
{
    public struct WatchExpression : ISnapshotLike
    {
        public string WatchExpressionFilePath { get; set; }

        public LineRange? LineRange { get; set; }

        public WatchExpression(string watchExpressionFilePath, LineRange? lineRange)
        {
            WatchExpressionFilePath = watchExpressionFilePath;
            LineRange = lineRange;
        }
        
        public string GetIdentifier()
        {
            //we need this because / is not good as file name
            
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