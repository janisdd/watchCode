using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using watchCode.model;
using watchCode.model.snapshots;

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
            List<(WatchExpression watchExpression, bool wasEqual, bool needToUpdateWatchExpression, Snapshot snapshot)>
                watchExpressions,
            Dictionary<string, List<CommentPattern>> knownFileExtensionsWithoutExtension,
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
                if (watchExpressions[i].watchExpression.DocFilePath !=
                    firstWatchExpressionInDocPosition.DocFilePath ||
                    watchExpressions[i].watchExpression.DocLineRange !=
                    firstWatchExpressionInDocPosition.DocLineRange)
                {
                    //these are in different files...
                    Logger.Error($"found wrong grouped watch expressions (same group but not for the same file " +
                                 $"or position)," +
                                 $"found: {watchExpressions[i].watchExpression.DocFilePath}, expected: " +
                                 $"{firstWatchExpressionInDocPosition.DocFilePath}, skipping group");

                    return false;
                }
            }

            //watched whole file if something changed that's really a change
            if (watchExpressions.Any(p =>
                p.watchExpression.LineRange == null || p.snapshot.LineRange == null))
                return false;

            FileInfo fileInfo;
            string absoluteFilePath =
                DynamicConfig.GetAbsoluteDocFilePath(firstWatchExpressionInDocPosition.DocFilePath);


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

            if (firstWatchExpressionInDocPosition.UsedCommentFormat == null)
            {
                Logger.Error($"no comment format was found for the (first) watch expression, " +
                             $"thus the comment cannot be properly updated, " +
                             $"doc file: {firstWatchExpressionInDocPosition.GetDocumentationLocation()}");
                return false;
            }

            #endregion


            int count = 1;
            string line;
            bool wroteComment = false;

            var initWatchExpressionString = initWatchExpressionKeywords.First();

            //if we use the original comment format we should be find 
            //because the we cannot get more lines... than the original 
            StringBuilder builder =
                new StringBuilder(firstWatchExpressionInDocPosition.UsedCommentFormat.StartCommentPart);

            builder.Append(" ");
            builder.Append(initWatchExpressionString);
            builder.Append(" ");

            //in one doc location there could be many watch expressions
            //anydevery watch expression could watch another file...

            var targetSourceFileGroups = watchExpressions
                .GroupBy(p => p.watchExpression.SourceFilePath)
                .ToList();

            int writtenLinesCount = 1;


            int possibleLinesToWriteComment = firstWatchExpressionInDocPosition.DocLineRange.GetLength();

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

                        builder.Append(tuple.watchExpression.SourceFilePath);
                        builder.Append(":");


                        if (tuple.wasEqual && tuple.needToUpdateWatchExpression)
                        {
                            var newLineRange = GetNewLineRange(tuple.watchExpression, tuple.snapshot);
                            builder.Append(newLineRange.ToShortString());

                            updateWatchExpressionsList.Add(
                                (tuple.watchExpression,
                                new WatchExpression(
                                    tuple.watchExpression.SourceFilePath, newLineRange,
                                    tuple.watchExpression.DocFilePath,
                                    tuple.watchExpression.DocLineRange,
                                    tuple.watchExpression.UsedCommentFormat.Clone())
                                ));
                        }
                        else
                        {
                            builder.Append(tuple.watchExpression.LineRange.ToShortString());
                        }

                        continue;
                    }

                    builder.Append(", ");

                    if (tuple.wasEqual && tuple.needToUpdateWatchExpression)
                    {
                        var newLineRange = GetNewLineRange(tuple.watchExpression, tuple.snapshot);
                        builder.Append(newLineRange.ToShortString());

                        updateWatchExpressionsList.Add(
                            (tuple.watchExpression,
                            new WatchExpression(
                                tuple.watchExpression.SourceFilePath, newLineRange,
                                tuple.watchExpression.DocFilePath,
                                tuple.watchExpression.DocLineRange,
                                tuple.watchExpression.UsedCommentFormat.Clone())
                            ));
                    }
                    else
                    {
                        builder.Append(tuple.watchExpression.LineRange.ToShortString());
                    }
                }
            }

            builder.Append(" ");
            builder.Append(firstWatchExpressionInDocPosition.UsedCommentFormat.EndCommentPart);

            string newWatchExpressionLine = builder.ToString();


            try
            {
                using (var fs = File.Open(absoluteFilePath, FileMode.Open, FileAccess.Read))
                {
                    var sr = new StreamReader(fs);

                    while ((line = sr.ReadLine()) != null)
                    {
                        //we checked before
                        // ReSharper disable once PossibleInvalidOperationException
                        if (count >= firstWatchExpressionInDocPosition.DocLineRange.Start &&
                            count <= firstWatchExpressionInDocPosition.DocLineRange.End)
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
                                    linesBuilder.AppendLine("");
                                }
                            }
                            else
                            {
                                linesBuilder.AppendLine(newWatchExpressionLine);

                                wroteComment = true;
                            }
                        }
                        else
                        {
                            linesBuilder.AppendLine(line);
                        }

                        count++;
                    }
                }

                //just overwrite the file
                using (var streamWriter =
                    new StreamWriter(File.Open(absoluteFilePath, FileMode.Truncate, FileAccess.Write)))
                {
                    streamWriter.Write(linesBuilder.ToString());
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


        public static LineRange GetNewLineRange(WatchExpression watchExpression, Snapshot snapshot)
        {
            var newLineRange = new LineRange(
                snapshot.LineRange.Start,
                snapshot.LineRange.End);

            return newLineRange;
        }
    }
}
