namespace watchCode.model
{
    public enum LogLevel
    {
        /// <summary>
        /// even errors are omitted
        /// </summary>
        None,
        Info,
        Warn,
        /// <summary>
        /// errors are always printed to std err
        /// </summary>
        Error
    }
}
