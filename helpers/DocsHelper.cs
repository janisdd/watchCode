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
        /// <remarks>
        /// note that writing to a file (updating a doc file) will insert new line characters based on the os!
        /// </remarks>
        /// </summary>
        /// <param name="watchExpressions">all watch expressions in the same doc file (and position in this file) 
        /// with the same watch expression</param>
        /// <param name="knownFileExtensionsWithoutExtension"></param>
        /// <param name="initWatchExpressionKeywords"></param>
        /// <param name="config"></param>
        /// <param name="updateWatchExpressionsList">list with the (and only) the update watch expressions</param>
        /// <returns></returns>
        public static bool UpdateWatchExpressionInDocFile(
            List<(WatchExpression watchExpression, bool wasEqual, Snapshot snapshot)> watchExpressions,
            Dictionary<string, List<(string start, string end)>> knownFileExtensionsWithoutExtension,
            List<string> initWatchExpressionKeywords,
            Config config,
            out List<(WatchExpression oldWatchExpression, WatchExpression updateWatchExpression)>
                updateWatchExpressionsList)
        {
            updateWatchExpressionsList =
                new List<(WatchExpression oldWatchExpression, WatchExpression updateWatchExpression)>();
            
            //stores the lines of the doc file 
            var linesBuilder = new StringBuilder();

            if (watchExpressions.Count == 0) return true;

            var firstWatchExpressionInDocPosition = watchExpressions.First().watchExpression;


            #region -- some checks

            //ensure all expressions are for the same file
            for (int i = 1; i < watchExpressions.Count; i++)
            {
                if (watchExpressions[i].watchExpression.DocumentationFilePath !=
                    firstWatchExpressionInDocPosition.DocumentationFilePath ||
                    watchExpressions[i].watchExpression.DocumentationLineRange !=
                    firstWatchExpressionInDocPosition.DocumentationLineRange)
                {
                    //these are in different files...
                    Logger.Error($"found wrong grouped watch expressions (same group but not for the same file " +
                                 $"or position)," +
                                 $"found: {watchExpressions[i].watchExpression.DocumentationFilePath}, expected: " +
                                 $"{firstWatchExpressionInDocPosition.DocumentationFilePath}, skipping group");

                    return false;
                }
            }

            //watched whole file if something changed that's really a change
            if (watchExpressions.Any(p =>
                p.watchExpression.LineRange == null || p.snapshot.LineRange == null ||
                p.snapshot.ReversedLineRange == null))
                return false;

            FileInfo fileInfo;
            string absoluteFilePath =
                DynamicConfig.GetAbsoluteFilePath(firstWatchExpressionInDocPosition.DocumentationFilePath);


            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"could not update watch expression in " +
                                 $"file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()} because file does not exist, " +
                                 $"skipping");

                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not access (to update) watch expression in file: " +
                    $"{firstWatchExpressionInDocPosition.GetDocumentationLocation()}, " +
                    $"error: {e.Message}, skipping");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fileInfo.Extension))
            {
                Logger.Error($"no file extension for watch expression in " +
                             $"file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()}, cannot update watch expression, " +
                             $"skipping");
                return false;
            }

            if (knownFileExtensionsWithoutExtension.TryGetValue(fileInfo.Extension.Substring(1),
                    out var tuples) == false)
            {
                Logger.Error($"unknown file extension for watch expression in " +
                             $"file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()}, cannot update watch expression, " +
                             $"skipping");
                return false;
            }

            if (tuples.Count == 0)
            {
                Logger.Error($"no comment syntax for file extension {fileInfo.Extension.Substring(1)} found, " +
                             $"cannot update watch expression in " +
                             $"file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()}, skipping");
                return false;
            }


            if (initWatchExpressionKeywords.Count == 0)
            {
                Logger.Error($"no init watch expression found, cannot update watch expression in" +
                             $"file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()}" +
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

            //in one doc location there could be many watch expressions
            //any every watch expression could watch another file...

            var targetSourceFileGroups = watchExpressions
                .GroupBy(p => p.watchExpression.WatchExpressionFilePath)
                .ToList();

            int writtenLinesCount = 1;
            int possibleLinesToWriteComment = firstWatchExpressionInDocPosition.DocumentationLineRange.GetLength();

            for (var i = 0; i < targetSourceFileGroups.Count; i++)
            {
                //now we have all expressions that watch the same source code file
                var watchExpressionAndSnapshotTuple = targetSourceFileGroups[i].ToList();

                //now all ranges for this source file

                for (int j = 0; j < watchExpressionAndSnapshotTuple.Count; j++)
                {
                    var tuple = watchExpressionAndSnapshotTuple[j];
                    if (j == 0)
                    {
                        if (i != 0)
                        {
                            builder.Append(", ");
                            // ReSharper disable once PossibleInvalidOperationException
                            if (writtenLinesCount < possibleLinesToWriteComment)
                            {
                                builder.AppendLine();
                                writtenLinesCount++;
                            }
                        }


                        builder.Append(tuple.watchExpression.WatchExpressionFilePath);
                        builder.Append(" ");


                        if (tuple.wasEqual &&
                            (tuple.snapshot.TriedBottomOffset || tuple.snapshot.TriedSearchFileOffset))
                        {
                            var newLineRange = GetNewLineRange(tuple.watchExpression, tuple.snapshot);
                            builder.Append(newLineRange.ToShortString());

                            updateWatchExpressionsList.Add(
                                (tuple.watchExpression,
                                new WatchExpression(
                                    tuple.watchExpression.WatchExpressionFilePath, newLineRange,
                                    tuple.watchExpression.DocumentationFilePath,
                                    tuple.watchExpression.DocumentationLineRange)
                                ));
                        }
                        else
                        {
                            builder.Append(tuple.watchExpression.LineRange.ToShortString());
                        }

                        continue;
                    }

                    builder.Append(", ");

                    if (tuple.wasEqual &&
                        (tuple.snapshot.TriedBottomOffset || tuple.snapshot.TriedSearchFileOffset))
                    {
                        var newLineRange = GetNewLineRange(tuple.watchExpression, tuple.snapshot);
                        builder.Append(newLineRange.ToShortString());

                        updateWatchExpressionsList.Add(
                            (tuple.watchExpression,
                            new WatchExpression(
                                tuple.watchExpression.WatchExpressionFilePath, newLineRange,
                                tuple.watchExpression.DocumentationFilePath,
                                tuple.watchExpression.DocumentationLineRange)
                            ));
                    }
                    else
                    {
                        builder.Append(tuple.watchExpression.LineRange.ToShortString());
                    }
                }
            }

            builder.Append(" ");
            builder.Append(commentFormat.end);

            string newWatchExpressionLine = builder.ToString();

            var absoluteTempFilePath =
                Path.Combine(DynamicConfig.GetAbsoluteWatchCodeDirPath(config), fileInfo.Name);

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
                        if (count >= firstWatchExpressionInDocPosition.DocumentationLineRange.Start &&
                            count <= firstWatchExpressionInDocPosition.DocumentationLineRange.End)
                        {
                            if (wroteComment)
                            {
                                if (writtenLinesCount - 1 > 0) //-1 because we already need to write at least 1 line
                                {
                                    //skipt the amount of lines we added
                                    writtenLinesCount--;
                                }
                                else
                                {
                                    //if we change the total lines of the doc file all other ranges will become invalid...
                                    if (config.UseInMemoryStringBuilderFileForUpdateingDocs.Value)
                                    {
                                        linesBuilder.AppendLine("");
                                    }
                                    else
                                    {
                                        sw.WriteLine();
                                    }
                                }
                            }
                            else
                            {
                                if (config.UseInMemoryStringBuilderFileForUpdateingDocs.Value)
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
                            if (config.UseInMemoryStringBuilderFileForUpdateingDocs.Value)
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

                if (config.UseInMemoryStringBuilderFileForUpdateingDocs.Value)
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
                    $"could not update watch expression in file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()}, " +
                    $"error: {e.Message}, skipping");
                return false;
            }

            return true;
        }

        public static LineRange GetNewLineRangeFromReverseLineRange(Snapshot snapshot)
        {
            return new LineRange(
                snapshot.TotalLinesInFile - snapshot.ReversedLineRange.End,
                snapshot.TotalLinesInFile - snapshot.ReversedLineRange.Start);
        }

        public static LineRange GetNewLineRange(WatchExpression watchExpression, Snapshot snapshot)
        {
            LineRange newLineRange = null;

            if (snapshot.TriedBottomOffset)
            {
                if (snapshot.ReversedLineRange == null)
                {
                    throw new ArgumentNullException($"to calculate the new line rang we need " +
                                                    $"{snapshot}.{snapshot.ReversedLineRange}");
                }

                newLineRange = new LineRange(
                    snapshot.TotalLinesInFile - snapshot.ReversedLineRange.End,
                    snapshot.TotalLinesInFile - snapshot.ReversedLineRange.Start);
            }

            else if (snapshot.TriedSearchFileOffset)
            {
                newLineRange = new LineRange(
                    snapshot.LineRange.Start,
                    snapshot.LineRange.End);
            }
            else
            {
                throw new NotImplementedException("tried to update docs but neither TriedBottomOffset " +
                                                  "nor TriedSearchFileOffset was used...");
            }


            return newLineRange;
        }
    }
}
