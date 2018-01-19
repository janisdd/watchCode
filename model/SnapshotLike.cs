namespace watchCode.model
{
    public interface ISnapshotLike
    {
        string WatchExpressionFilePath { get; set; }
        LineRange? LineRange { get; set; }

        /// <summary>
        /// returns a unique identifier based on <see cref="WatchExpressionFilePath"/> and <see cref="LineRange"/>
        /// </summary>
        /// <returns></returns>
        string GetSnapshotFileNameWithoutExtension(bool combinedSnapshotFiles);
        
        
    }
}