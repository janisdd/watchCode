namespace watchCode.model
{
    public interface ISnapshotLike
    {
        /// <summary>
        /// the file path found in the watch expression
        /// </summary>
        string SourceFilePath { get; set; }
        
        /// <summary>
        /// returns a unique identifier based on <see cref="SourceFilePath"/> and <see cref="LineRange"/>
        /// </summary>
        /// <returns></returns>
        string GetSnapshotFileNameWithoutExtension();
        
        
    }
}
