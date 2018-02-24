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
        /// updates the given watch expressions
        /// if not exist then it will be created
        /// </summary>
        Update,

        /// <summary>
        /// updates all watch expressions
        /// if not exist then it will be created
        /// </summary>
        UpdateAll,

        /// <summary>
        /// compare the stored snapshots with the expression results
        /// </summary>
        Compare,

        /// <summary>
        /// compares and updates the doc files automatically if possible
        /// </summary>
        CompareAndUpdateDocs,

        /// <summary>
        /// updates all watch expressions matching the old expressions with the new expression
        /// (and updates the snapshots)
        /// </summary>
        UpdateExpression,
        
        /// <summary>
        /// when we update expressions we don't touch the old snapshot files because other expressions could reference
        /// them (TODO always collect all expression then we could delete them immediately)
        /// 
        /// this clears all snapshots that are not referenced by any expression
        /// </summary>
        ClearUnusedSnapshots
    }
}
