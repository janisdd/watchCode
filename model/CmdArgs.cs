using System.Collections.Generic;
using watchCode.helpers;

namespace watchCode.model
{
    /// <summary>
    /// all props are possible params prepended with - and camelCase
    /// order does not matter
    /// </summary>
    public class CmdArgs
    {

        #region only cmd args

       
        /// <summary>
        /// config file to use inside root dir
        /// </summary>
        public string ConfigFileNameWithExtension { get; set; }


        /// <summary>
        /// true: creates all snapshots (if not already exist)
        /// so especially not overwriting anything
        /// </summary>
        public bool Init { get; set; }
        
        /// <summary>
        /// true: updates the all snapshots
        /// if not exist then it will be created
        /// </summary>
        public bool Update { get; set; }
        
        /// <summary>
        /// true: compare the stored snapshots with the expression results
        /// </summary>
        public bool Compare { get; set; }

        /// <summary>
        /// true: updates the doc file watch expression if the watched lines change
        /// but bottom to top searching found the right lines 
        /// </summary>
        public bool CompareAndUpdateDocs { get; set; }

        
        #endregion
    }
}