using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using watchCode.model;

namespace watchCode.helpers
{
    public static class DumpWatchExpressionHelper
    {
        public static bool DumpWatchExpressions(string absoluteDumpFilePath, List<WatchExpression> watchExpressions,
            bool prettyDump = false)
        {
            string watchExpressionsSerialized =
                JsonConvert.SerializeObject(watchExpressions, prettyDump ? Formatting.Indented : Formatting.None);

            try
            {
                var fileInfo = new FileInfo(absoluteDumpFilePath);

                if (fileInfo.Directory.Exists == false)
                {
                    Logger.Info($"dump file parent directory does not exist, creating full path...");
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                    Logger.Info($"... finished creating directory full path for dum file");
                }
                
                File.WriteAllText(fileInfo.FullName, watchExpressionsSerialized);
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not dump watch expressions to file: {absoluteDumpFilePath}, error: {e.Message}, aborting");
                return false;
            }

            Logger.Info($"dumped {watchExpressions.Count} watch expression(s) into file: {absoluteDumpFilePath}");

            return true;
        }
    }
}
