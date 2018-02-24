namespace watchCode.model.snapshots
{
    public interface ISnapshot
    {
        /// <summary>
        /// the watched file path (relative)
        /// </summary>
        string SourceFilePath { get; set; }
    }
}
