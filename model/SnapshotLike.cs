namespace watchCode.model
{
    public interface ISnapshotLike
    {
        /// <summary>
        /// the file path found in the watch expression
        /// </summary>
        string WatchExpressionFilePath { get; set; }
        
        /// <summary>
        /// if this is null then we watch the whole file
        /// </summary>
        LineRange LineRange { get; set; }

        /// <summary>
        /// returns a unique identifier based on <see cref="WatchExpressionFilePath"/> and <see cref="LineRange"/>
        /// </summary>
        /// <returns></returns>
        string GetSnapshotFileNameWithoutExtension(bool combinedSnapshotFiles);
        
        
    }
}