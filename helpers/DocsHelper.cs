using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using watchCode.model;

namespace watchCode.helpers
{
    public static class DocsHelper
    {
        
        
        /// <summary>
        /// 
        /// <remarks>
        /// note that writing to a file (updating a doc file) will insert new line characters based on the os!
        /// </remarks>
        /// 
        /// </summary>
        /// <param name="watchExpressions">all watch expressions in the same doc file (and position in this file) 
        /// with the same watch expression</param>
        /// <param name="knownFileExtensionsWithoutExtension"></param>
        /// <param name="initWatchExpressionKeywords"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static bool UpdateWatchExpressionInDocFile(
            List<(WatchExpression watchExpression, Snapshot snapshot)> watchExpressions,
            Dictionary<string, List<(string start, string end)>> knownFileExtensionsWithoutExtension,
            List<string> initWatchExpressionKeywords,
            Config config)
        {
            
           var linesBuilder = new StringBuilder();

            if (watchExpressions.Count == 0) return true;

            var firstWatchExpression = watchExpressions.First().watchExpression;


            #region -- some checks

            //ensure all expressions are for the same file
            for (int i = 1; i < watchExpressions.Count; i++)
            {
                if (watchExpressions[i].watchExpression.DocumentationFilePath !=
                    firstWatchExpression.DocumentationFilePath ||
                    watchExpressions[i].watchExpression.DocumentationLineRange !=
                    firstWatchExpression.DocumentationLineRange)
                {
                    //these are in different files...
                    Logger.Error($"found wrong grouped watch expressions (same group but not for the same file " +
                                 $"or position)," +
                                 $"found: {watchExpressions[i].watchExpression.DocumentationFilePath}, expected: " +
                                 $"{firstWatchExpression.DocumentationFilePath}, skipping group");

                    return false;
                }
            }

            //watched whole file if something changed that's really a change
            if (watchExpressions.Any(p =>
                p.watchExpression.LineRange == null || p.snapshot.LineRange == null ||
                p.snapshot.ReversedLineRange == null))
                return false;

            FileInfo fileInfo;
            string absoluteFilePath = DynamicConfig.GetAbsoluteFilePath(firstWatchExpression.DocumentationFilePath);


            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"could not update watch expression in " +
                                 $"file: {firstWatchExpression.GetDocumentationLocation()} because file does not exists, " +
                                 $"skipping");

                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not access (to update) watch expression in file: " +
                    $"{firstWatchExpression.GetDocumentationLocation()}, " +
                    $"error: {e.Message}, skipping");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fileInfo.Extension))
            {
                Logger.Error($"no file extension for watch expression in " +
                             $"file: {firstWatchExpression.GetDocumentationLocation()}, cannot update watch expression, " +
                             $"skipping");
                return false;
            }

            if (knownFileExtensionsWithoutExtension.TryGetValue(fileInfo.Extension.Substring(1),
                    out var tuples) == false)
            {
                Logger.Error($"unknown file extension for watch expression in " +
                             $"file: {firstWatchExpression.GetDocumentationLocation()}, cannot update watch expression, " +
                             $"skipping");
                return false;
            }

            if (tuples.Count == 0)
            {
                Logger.Error($"no comment syntax for file extension {fileInfo.Extension.Substring(1)} found, " +
                             $"cannot update watch expression in " +
                             $"file: {firstWatchExpression.GetDocumentationLocation()}, skipping");
                return false;
            }


            if (initWatchExpressionKeywords.Count == 0)
            {
                Logger.Error($"no init watch expression found, cannot update watch expression in" +
                             $"file: {firstWatchExpression.GetDocumentationLocation()}" +
                             $"skipping");
                return false;
            }

            #endregion


            int count = 1;
            string line;
            bool wroteComment = false;

            var commentFormat = tuples.First();
            var initWatchExpressionString = initWatchExpressionKeywords.First();

            StringBuilder builder = new StringBuilder(commentFormat.start);

            builder.Append(" ");
            builder.Append(initWatchExpressionString);
            builder.Append(" ");
            builder.Append(firstWatchExpression.WatchExpressionFilePath);
            builder.Append(" ");

            for (var i = 0; i < watchExpressions.Count; i++)
            {
                var watchExpressionAndSnapshotTuple = watchExpressions[i];
                var lineRange = GetNewLineRange(watchExpressionAndSnapshotTuple.watchExpression,
                    watchExpressionAndSnapshotTuple.snapshot);

                if (i == 0)
                {
                    builder.Append(lineRange.ToShortString());
                    continue;
                }

                builder.Append(lineRange.ToShortString());
                builder.Append(", ");
            }

            builder.Append(" ");
            builder.Append(commentFormat.end);

            string newWatchExpressionLine = builder.ToString();

            var absoluteTempFilePath =
                Path.Combine(DynamicConfig.GetAbsoluteWatchCodeDirPath(config.WatchCodeDirName), fileInfo.Name);

            try
            {
                StreamWriter sw = null;

                using (var fs = File.Open(absoluteFilePath, FileMode.Open, FileAccess.Read))
                {
                    var sr = new StreamReader(fs);

                    if (config.UseInMemoryStringBuilderFileForUpdateingDocs == false)
                        sw = new StreamWriter(new FileStream(absoluteTempFilePath, FileMode.Create));

                    while ((line = sr.ReadLine()) != null)
                    {
                        //we checked before
                        // ReSharper disable once PossibleInvalidOperationException
                        if (count >= firstWatchExpression.DocumentationLineRange.Start &&
                            count <= firstWatchExpression.DocumentationLineRange.End)
                        {
                            if (wroteComment)
                            {
                                //if we change the total lines of the doc file all other ranges will become invalid...
                                if (config.UseInMemoryStringBuilderFileForUpdateingDocs)
                                {
                                    linesBuilder.AppendLine("");
                                }
                                else
                                {
                                    sw.WriteLine();
                                }
                            }
                            else
                            {
                                if (config.UseInMemoryStringBuilderFileForUpdateingDocs)
                                {
                                    linesBuilder.AppendLine(newWatchExpressionLine);
                                }
                                else
                                {
                                    sw.WriteLine(newWatchExpressionLine);
                                }
                                wroteComment = true;
                            }
                        }
                        else
                        {
                            if (config.UseInMemoryStringBuilderFileForUpdateingDocs)
                            {
                                linesBuilder.AppendLine(line);
                            }
                            else
                            {
                                sw.WriteLine(line);
                            }
                        }

                        count++;
                    }
                }

                if (config.UseInMemoryStringBuilderFileForUpdateingDocs)
                {
                    //just overwrite the file
                    using (var streamWriter =
                        new StreamWriter(File.Open(absoluteFilePath, FileMode.Truncate, FileAccess.Write)))
                    {
                        streamWriter.Write(linesBuilder.ToString());
                    }
                }
                else
                {
                    sw.Dispose();
                    //temp file created
                    fileInfo.Refresh();
                    File.Delete(fileInfo.FullName);
                    File.Move(absoluteTempFilePath, fileInfo.FullName);
                    
//                    not sure about this ... do we need to set the creation time / last accessed time?
//                    var newFileInfo = new FileInfo(fileInfo.FullName);
//                    newFileInfo.Attributes = fileInfo.Attributes;

                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not update watch expression in file: {firstWatchExpression.GetDocumentationLocation()}, " +
                    $"error: {e.Message}, skipping");
                return false;
            }

            return true;
        }

        public static LineRange GetNewLineRange(WatchExpression watchExpression, Snapshot snapshot)
        {
            if (snapshot.ReversedLineRange == null)
            {
                throw new ArgumentNullException($"to calculate the new line rang we need " +
                                                $"{snapshot}.{snapshot.ReversedLineRange}");
            }
            ;

            LineRange newLineRange = new LineRange(
                snapshot.TotalLines - snapshot.ReversedLineRange.Value.End,
                snapshot.TotalLines - snapshot.ReversedLineRange.Value.Start);

            return newLineRange;
        }
    }
}