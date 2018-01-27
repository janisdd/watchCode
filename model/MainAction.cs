namespace watchCode.model
{
    public enum MainAction
    {
        /// <summary>
        /// do nothing ... e.g. display help
        /// </summary>
        None,
        /// <summary>
        /// creates all snapshots (if not already exist)
        /// so especially not overwriting anything
        /// </summary>
        Init,
        /// <summary>
        /// updates the all snapshots
        /// if not exist then it will be created
        /// </summary>
        Update,
        /// <summary>
        /// compare the stored snapshots with the expression results
        /// </summary>
        Compare
    }
}
