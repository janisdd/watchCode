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
        /// true: 
        /// </summary>
        public MainAction MainAction { get; set; }

        /// <summary>
        /// true: updates the doc file watch expression if the watched lines change
        /// but bottom to top searching found the right lines 
        /// </summary>
        public bool CompareAndUpdateDocs { get; set; }

        /// <summary>
        /// writes the log inside the watch code dir
        /// </summary>
        public bool WriteLog { get; set; }


        
        #endregion
    }
}
