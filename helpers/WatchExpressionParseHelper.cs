using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using watchCode.model;

namespace watchCode.helpers
{
    public static class WatchExpressionParseHelper
    {
        public static readonly string CommentContentPattern = $"[\\s\\S\n]*?";

        public static List<WatchExpression> GetAllWatchExpressions(FileInfo docFileInfo,
            Dictionary<string, List<(string start, string end)>> knownFileExtensionsWithoutExtension,
            List<string> initWatchExpressionKeywords)
        {
            
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            
            List<WatchExpression> watchExpressions = new List<WatchExpression>();

            string fileContent = File.ReadAllText(docFileInfo.FullName);

            if (knownFileExtensionsWithoutExtension.TryGetValue(docFileInfo.Extension.Substring(1),
                    out var commentPatterns) == false)
            {
                Logger.Info($"file extension is unknown, skipping file: {docFileInfo.FullName}");
                return new List<WatchExpression>();
            }


            foreach (var commentPattern in commentPatterns)
            {
                var matches = Regex.Matches(fileContent,
                    $"{commentPattern.start}{CommentContentPattern}{commentPattern.end}");

                for (int i = 0; i < matches.Count; i++)
                {
                    var commentMatch = matches[i];

                    var expressionLocation =
                        GetLineRangeFromIndices(fileContent, commentMatch.Index, commentMatch.Length);

                    if (expressionLocation == null)
                    {
                        Logger.Warn(
                            $"could not get watch expression num: {i + 1}. expression in doc file: {docFileInfo.FullName}, skipping");
                        continue;
                    }


                    var commentText = commentMatch.Value.Substring(commentPattern.start.Length,
                            commentMatch.Value.Length - commentPattern.end.Length - commentPattern.start.Length)
                        .Trim();

                    foreach (var watchExpressionKeyword in initWatchExpressionKeywords)
                    {
                        if (commentText.StartsWith(watchExpressionKeyword))
                        {
                            var plainExpressionText = commentText.Substring(watchExpressionKeyword.Length);

                            var expressions = GetWatchExpressions(plainExpressionText, docFileInfo,
                                expressionLocation);
                            watchExpressions.AddRange(expressions);
                        }
                    }
                }
            }

            stopwatch.Stop();
            
            Logger.Info($"parsed file {docFileInfo.FullName} (contained {watchExpressions.Count} expression(s)) " +
                        $"in {StopWatchHelper.GetElapsedTime(stopwatch)}");
            
            return watchExpressions;
        }

        private static LineRange GetLineRangeFromIndices(string text, int startIndex, int length)
        {
            int line = 1;
            int startLine = -1;
            int endLine = -1;

            //text[startIndex] is exatcly the first char of the match 
            for (int i = 0; i < startIndex + length; i++)
            {
                if (i == startIndex) startLine = line;
                if (text[i] == '\n') line++; //TODO \r\n for windows
            }
            //the last one is text[startIndex + length - 1] that is exactly the last char of the match...
            endLine = line;

            if (startLine < 0 || endLine < 0)
            {
                return null;
            }

            return new LineRange(startLine, endLine);
        }

        private static List<WatchExpression> GetWatchExpressions(string watchExpressionString, FileInfo fileInfo,
            LineRange watchExpressionFoundLineRange)
        {
            /*
             * different formats allowed:
             *
             * all spaces can be new lines or multiple whitespaces
             *
             * watch expression:
             * 
             * [path] - only check if file exists (must be a file, not a dir)
             * [path] [int] - watch specific line inside file
             * [path] [int] - [int] watch specific line inside file (int 1 must >= int 2),
             *     if equal then the same as 1 int
             *
             * [watch expression] (, [watch expression])*
             *
             *
             * currently one the simple format with single line range is supported
             *
             * --- constraints ---
             * [int] must be >= 0
             * [path] must not contain , if it contains spaces then enclosed in "..." else they will probably truncated
             * only one " is not allowed
             */

            List<WatchExpression> watchExpressions = new List<WatchExpression>();

            var possibleWatchExpressionStrings =
                watchExpressionString.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries);


            WatchExpression previousWatchExpression = null;
            foreach (var expressionString in possibleWatchExpressionStrings)
            {
                var watchExpresion =
                    ParseSingleWatchExpression(expressionString, fileInfo, watchExpressionFoundLineRange,
                        previousWatchExpression);

                if (watchExpresion == null) continue;

                watchExpressions.Add(watchExpresion);
                previousWatchExpression = watchExpresion;
            }


            return watchExpressions;
        }


        private static WatchExpression ParseSingleWatchExpression(string possibleWatchExpression, FileInfo fileInfo,
            LineRange watchExpressionFoundLineRange, WatchExpression previousWatchExpression)
        {
            possibleWatchExpression = possibleWatchExpression.Trim();

            bool containsEscape = false;
            bool isReadingPath = true;

            StringBuilder builder = new StringBuilder();


            string documentationFileRelativePath =
                IoHelper.GetRelativePath(fileInfo.FullName, DynamicConfig.AbsoluteRootDirPath);


            for (int i = 0; i < possibleWatchExpression.Length; i++)
            {
                var ch = possibleWatchExpression[i];

                if (ch == '"' && containsEscape == false) containsEscape = true;

                if (ch == '"' && containsEscape) containsEscape = false; //we matched the before "

                if (ch == ' ')
                {
                    if (containsEscape) // " xxx "
                    {
                        //nothing special here
                    }
                    else
                    {
                        //we found splitting point //path int
                        isReadingPath = false;
                        continue;
                    }
                }

                if (isReadingPath) builder.Append(ch);
            }

            if (containsEscape)
            {
                //we found a " without a matching "...
                Logger.Warn(
                    $"found orphan \" in the watch expression: {possibleWatchExpression} in file: {fileInfo.FullName}, skipping");
                return null;
            }


            LineRange lineRang = null;
            var filePath = builder.ToString();

            if (filePath.Length == 0)
            {
                Logger.Warn(
                    $"found empty watch expression: {possibleWatchExpression} in file: {fileInfo.FullName}, skipping");
                return null;
            }

            lineRang = ParseLineRange(filePath, fileInfo, true);

            if (lineRang != null)
            {
                //the path is a line range so take the file path of the previous watch expression 
                if (previousWatchExpression == null)
                {
                    Logger.Warn(
                        $"found invalid watch expression (only line range): {possibleWatchExpression} in file: {fileInfo.FullName}, skipping");
                    return null;
                }

                filePath = previousWatchExpression.WatchExpressionFilePath;

                //make sure the doc file position is the same
                return new WatchExpression(filePath, lineRang, documentationFileRelativePath,
                    new LineRange(previousWatchExpression.DocumentationLineRange.Start,
                        previousWatchExpression.DocumentationLineRange.End));
            }

            var lineRangString = possibleWatchExpression.Substring(builder.Length);

            //watch expression can only be a file --> watch all lines

            if (string.IsNullOrWhiteSpace(lineRangString) == false) //we have specific line(s) to watch
            {
                lineRang = ParseLineRange(lineRangString, fileInfo, false);

                if (lineRang == null) return null;
            }

            return new WatchExpression(filePath, lineRang, documentationFileRelativePath,
                watchExpressionFoundLineRange);
        }

        private static LineRange ParseLineRange(string possibleLineRang, FileInfo fileInfo, bool suppressWarnings)
        {
            possibleLineRang = possibleLineRang.Trim();

            //can be single line e.g. 6
            //or a range e.g. 0-6

            if (possibleLineRang.Contains("-"))
            {
                var rangeStrings = possibleLineRang.Split(new char[] {'-'}, StringSplitOptions.RemoveEmptyEntries);
                int start;
                int end;

                if (rangeStrings.Length != 2)
                {
                    if (suppressWarnings == false)
                        Logger.Warn(
                            $"found invalid line range expression (too many/too few values): {possibleLineRang} in file: {fileInfo.FullName}, skipping");

                    return null;
                }

                if (int.TryParse(rangeStrings[0], out start) == false)
                {
                    if (suppressWarnings == false)
                        Logger.Warn(
                            $"found invalid line range expression (start value): {possibleLineRang} in file: {fileInfo.FullName}, skipping");

                    return null;
                }

                if (start < 0)
                {
                    if (suppressWarnings == false)
                        Logger.Warn(
                            $"found invalid (negative) line range expression (start value): {possibleLineRang} in file: {fileInfo.FullName}, skipping");

                    return null;
                }

                if (int.TryParse(rangeStrings[1], out end) == false)
                {
                    if (suppressWarnings == false)
                        Logger.Warn(
                            $"found invalid line range expression (end value): {possibleLineRang} in file: {fileInfo.FullName}, skipping");

                    return null;
                }

                if (end < 0)
                {
                    if (suppressWarnings == false)
                        Logger.Warn(
                            $"found invalid (negative) line range expression (end value): {possibleLineRang} in file: {fileInfo.FullName}, skipping");

                    return null;
                }

                return new LineRange(start, end);
            }

            //single value
            int startAndEnd;

            if (int.TryParse(possibleLineRang, out startAndEnd) == false)
            {
                if (suppressWarnings == false)
                Logger.Warn(
                    $"found invalid line (< 0) range expression (start value): {possibleLineRang} in file: {fileInfo.FullName}, skipping");
                
                return null;
            }

            if (startAndEnd < 0)
            {
                if (suppressWarnings == false)
                Logger.Warn(
                    $"found invalid (< 0) line range expression: {possibleLineRang} in file: {fileInfo.FullName}, skipping");
                
                return null;
            }

            return new LineRange(startAndEnd);
        }
    }
}
