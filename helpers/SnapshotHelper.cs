using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using watchCode.model;

namespace watchCode.helpers
{
    public static class SnapshotHelper
    {
        public static string SnapshotFileExtension = "json";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="absoluteFilePath"></param>
        /// <param name="watchExpression"></param>
        /// <param name="compressLines">true: compress to one line using md5 hash, false: use plaing lines</param>
        /// <returns></returns>
        public static Snapshot CreateSnapshot(string absoluteFilePath, WatchExpression watchExpression,
            bool compressLines, bool alsoUseReverseLines)
        {
            FileInfo fileInfo;

            #region --- some checks

            if (watchExpression.LineRange.HasValue &&
                (watchExpression.LineRange.Value.Start <= 0 || watchExpression.LineRange.Value.End <= 0 ||
                 watchExpression.LineRange.Value.End < watchExpression.LineRange.Value.Start))
            {
                //this can happen if the file is e.g. renamed... so jsut a warning
                //the compare will be ok if because the new snapshot will be null
                Logger.Warn(
                    $"could not create snapshot for file: {absoluteFilePath} because line range was " +
                    $"invalide: start: {watchExpression.LineRange.Value.Start}, end: {watchExpression.LineRange.Value.End}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }


            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"could create snapshot for file: {absoluteFilePath} because file not exists, " +
                                 $"at doc file: {watchExpression.GetDocumentationLocation()}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could not create file info for file: {absoluteFilePath}, error: {e.Message}, " +
                             $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            #endregion

            //file exists

            List<string> lines = new List<string>();

            int count = 1;
            string line;

            IndexRange lineRangeIndices = new IndexRange();
            IndexRange reversedLineRangeIndices = new IndexRange();
            LineRange reversedLineRange = new LineRange();

            try
            {
                if (compressLines)
                {
                    #region --- compress to one line

                    if (watchExpression.LineRange == null)
                    {
                        lines.Add(HashHelper.GetHashForFile(fileInfo));
                    }
                    else
                    {
                        StringBuilder builder = new StringBuilder();

                        using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                        {
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (alsoUseReverseLines)
                                {
                                    //iterate all lines because we need to know the total number of lines
                                }
                                else
                                {
                                    //we got all lines
                                    break;
                                }

                                if (count == watchExpression.LineRange.Value.Start - 1)
                                {
                                    //use the line before because the read aready consume the prev line + new lien char
                                    long pos = IoHelper.GetActualPosition(sr);
                                    lineRangeIndices.Start = pos;
                                    reversedLineRangeIndices.End = fileInfo.Length - pos;
                                }

                                if (count == watchExpression.LineRange.Value.End)
                                {
                                    long pos = IoHelper.GetActualPosition(sr);
                                    lineRangeIndices.End = pos;
                                    reversedLineRangeIndices.Start = fileInfo.Length - pos;
                                }

                                if (count >= watchExpression.LineRange.Value.Start &&
                                    count <= watchExpression.LineRange.Value.End)
                                    builder.Append(line);

                                count++;
                            }
                        }

                        string concatLines = builder.ToString();
                        lines.Add(HashHelper.GetHash(concatLines));

                        if (watchExpression.LineRange != null && alsoUseReverseLines)
                        {
                            reversedLineRange.Start = count - watchExpression.LineRange.Value.End;
                            reversedLineRange.End = count - watchExpression.LineRange.Value.Start;
                        }
                    }

                    #endregion
                }
                else
                {
                    //use plain lines (compressLines == false)

                    #region --- plain lines

                    using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                    {
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (watchExpression.LineRange == null)
                            {
                                //add all files
                                lines.Add(line);
                            }
                            else
                            {
                                if (count > watchExpression.LineRange.Value.End)
                                {
                                    if (alsoUseReverseLines)
                                    {
                                        //iterate all lines because we need to know the total number of lines
                                    }
                                    else
                                    {
                                        //we got all lines
                                        break;
                                    }
                                }

                                if (count == watchExpression.LineRange.Value.Start - 1)
                                {
                                    //use the line before because the read aready consume the prev line + new lien char
                                    long pos = IoHelper.GetActualPosition(sr);
                                    lineRangeIndices.Start = pos;
                                    reversedLineRangeIndices.End = fileInfo.Length - pos;
                                }

                                if (count == watchExpression.LineRange.Value.End)
                                {
                                    long pos = IoHelper.GetActualPosition(sr);
                                    lineRangeIndices.End = pos;
                                    reversedLineRangeIndices.Start = fileInfo.Length - pos;
                                }


                                if (count >= watchExpression.LineRange.Value.Start &&
                                    count <= watchExpression.LineRange.Value.End) lines.Add(line);
                            }
                            count++;
                        }
                    }

                    if (watchExpression.LineRange != null && alsoUseReverseLines)
                    {
                        reversedLineRange.Start = count - watchExpression.LineRange.Value.End;
                        reversedLineRange.End = count - watchExpression.LineRange.Value.Start;
                    }

                    #endregion
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFilePath}, error (during read): {e.Message}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            return new Snapshot(watchExpression.WatchExpressionFilePath, watchExpression.LineRange,
                watchExpression.LineRange != null && alsoUseReverseLines ? reversedLineRange : (LineRange?) null,
                watchExpression.LineRange.HasValue ? lineRangeIndices : (IndexRange?) null,
                watchExpression.LineRange.HasValue ? reversedLineRangeIndices : (IndexRange?) null,
                lines);
        }


     
        //this also checks the bottom offset lines...
        //actually
        public static Snapshot CreateSnapshotBasedOnOldSnapshot(string absoluteFilePath,
            WatchExpression watchExpression,
            bool compressLines, Snapshot oldSnapshot)
        {
            FileInfo fileInfo;

            #region --- some checks

            if (watchExpression.LineRange == null)
            {
                Logger.Error($"tried to create a snapshot based on an old one but for the whole file, use " +
                             $"{nameof(SnapshotHelper.CreateSnapshot)} instead, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return null;
            }

            if (watchExpression.LineRange.Value.Start <= 0 || watchExpression.LineRange.Value.End <= 0 ||
                watchExpression.LineRange.Value.End < watchExpression.LineRange.Value.Start)
            {
                //this can happen if the file is e.g. renamed... so jsut a warning
                //the compare will be ok if because the new snapshot will be null
                Logger.Warn(
                    $"could not create snapshot for file: {absoluteFilePath} because line range was " +
                    $"invalide: start: {watchExpression.LineRange.Value.Start}, end: {watchExpression.LineRange.Value.End}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            if (oldSnapshot.ReversedLineRange == null)
            {
                Logger.Error(
                    $"tried to create a snapshot based on an old one but reverse ranges are not set, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return null;
            }

            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"could create snapshot for file: {absoluteFilePath} because file not exists, " +
                                 $"at doc file: {watchExpression.GetDocumentationLocation()}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could not create file info for file: {absoluteFilePath}, error: {e.Message}, " +
                             $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            #endregion

            //file exists

            string line;
            int count = 1;

            var newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath, watchExpression.LineRange,
                oldSnapshot.ReversedLineRange,
                oldSnapshot.LineRangeIndices, oldSnapshot.ReverseLineIndices, new List<string>());

            try
            {
                if (compressLines)
                {
                    #region --- compress to one line

                    StringBuilder builder = new StringBuilder();

                    using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                    {
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (count >= watchExpression.LineRange.Value.Start &&
                                count <= watchExpression.LineRange.Value.End)
                            {
                                builder.Append(line);
                            }

                            if (count > watchExpression.LineRange.Value.End) break;

                            count++;
                        }
                    }

                    string concatLines = builder.ToString();
                    string hash = HashHelper.GetHash(concatLines);
                    newSnapshot.Lines = new List<string>() {hash};

                    var areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                    if (areEqual == false)
                    {
                        //maybe the user only inserted lines above... check from bottom
                        builder.Clear();
                        count = 1;

                        newSnapshot.UsedBottomOffset = true;
                        Logger.Info($"created snapshot from old snapshot (from start) and was not equal, " +
                                    $"snapshot: {watchExpression.WatchExpressionFilePath}, " +
                                    $"doc file: {watchExpression.GetDocumentationLocation()}, " +
                                    $"using file bottom offset...");


                        {
                            var sr = new ReverseLineReader(absoluteFilePath);

                            foreach (var _line in sr)
                            {
                                if (count >= watchExpression.LineRange.Value.Start &&
                                    count <= watchExpression.LineRange.Value.End)
                                {
                                    builder.Append(_line);
                                }

                                if (count > watchExpression.LineRange.Value.End) break;

                                count++;
                            }
                        }

                        concatLines = builder.ToString();
                        hash = HashHelper.GetHash(concatLines);
                        newSnapshot.Lines = new List<string>() {hash};
                    }

                    #endregion
                }
                else
                {
                    //use plain lines (compressLines == false)
                    var lines = new string[watchExpression.LineRange.Value.GetLength()];

                    #region --- plain lines

                    using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                    {
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (count >= watchExpression.LineRange.Value.Start &&
                                count <= watchExpression.LineRange.Value.End)
                            {
                                lines[count - watchExpression.LineRange.Value.Start] = line;
                            }


                            if (count > watchExpression.LineRange.Value.End) break;

                            count++;
                        }
                    }

                    newSnapshot.Lines = lines.ToList();

                    var areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                    if (areEqual == false)
                    {
                        //maybe the user only inserted lines above... check from bottom

                        newSnapshot.UsedBottomOffset = true;
                        Logger.Info($"created snapshot from old snapshot (from start) and was not equal, " +
                                    $"snapshot: {watchExpression.WatchExpressionFilePath}, " +
                                    $"doc file: {watchExpression.GetDocumentationLocation()}, " +
                                    $"using file bottom offset...");


                        count = 1;
                        lines = new string[oldSnapshot.ReversedLineRange.Value.GetLength()];

                        {
                            var sr = new ReverseLineReader(absoluteFilePath);

                            foreach (var _line in sr)
                            {
                                if (count >= oldSnapshot.ReversedLineRange.Value.Start &&
                                    count <= oldSnapshot.ReversedLineRange.Value.End)
                                {
                                    lines[lines.Length - (count - oldSnapshot.ReversedLineRange.Value.Start) - 1] = _line;
                                }

                                if (count > oldSnapshot.ReversedLineRange.Value.End) break;

                                count++;
                            }
                        }
                        newSnapshot.Lines = lines.ToList();
                    }

                    #endregion
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFilePath}, error (during read): {e.Message}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            return newSnapshot;
        }

        //this might be faster (even with reflection?)
        // but don't work if we change/add lines before & after the watched region... 
        //because we use fixed indices they change even if text is inserted in old lines...
        public static Snapshot CreateSnapshotBasedOnOldSnapshotWithIndices(string absoluteFilePath,
            WatchExpression watchExpression,
            bool compressLines, Snapshot oldSnapshot)
        {
            FileInfo fileInfo;

            #region --- some checks

            if (watchExpression.LineRange.HasValue == false)
            {
                Logger.Error($"tried to create a snapshot based on an old one but for the whole file, use " +
                             $"{nameof(SnapshotHelper.CreateSnapshot)} instead, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return null;
            }

            if (watchExpression.LineRange.Value.Start <= 0 || watchExpression.LineRange.Value.End <= 0 ||
                watchExpression.LineRange.Value.End < watchExpression.LineRange.Value.Start)
            {
                //this can happen if the file is e.g. renamed... so jsut a warning
                //the compare will be ok if because the new snapshot will be null
                Logger.Warn(
                    $"could not create snapshot for file: {absoluteFilePath} because line range was " +
                    $"invalide: start: {watchExpression.LineRange.Value.Start}, end: {watchExpression.LineRange.Value.End}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            if (oldSnapshot.LineRangeIndices == null || oldSnapshot.ReverseLineIndices == null)
            {
                Logger.Error($"tried to create a snapshot based on an old one but indices are not set, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return null;
            }

            if (oldSnapshot.LineRangeIndices.Value.GetLength() != oldSnapshot.ReverseLineIndices.Value.GetLength())
            {
                Logger.Error(
                    "indice ranges from start and ending don't have the same length, this will fail, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return null;
            }

            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"could create snapshot for file: {absoluteFilePath} because file not exists, " +
                                 $"at doc file: {watchExpression.GetDocumentationLocation()}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could not create file info for file: {absoluteFilePath}, error: {e.Message}, " +
                             $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            #endregion

            //file exists

            string line;

            var lineRangeIndices = oldSnapshot.LineRangeIndices;
            var reversedLineRangeIndices = oldSnapshot.ReverseLineIndices;

            var newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath, watchExpression.LineRange,
                null,
                lineRangeIndices, reversedLineRangeIndices, new List<string>());

            StringBuilder builder = new StringBuilder();

            try
            {
                if (compressLines)
                {
                    #region --- compress to one line

                    char[] buff = new char[lineRangeIndices.Value.GetLength()];

                    #region --- first try from top

                    using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                    {
                        sr.BaseStream.Seek(lineRangeIndices.Value.Start, SeekOrigin.Begin);
                        sr.ReadBlock(buff, 0, (int) lineRangeIndices.Value.GetLength());

                        //now remove the new line chars...
                        GetTextFromBuffer(buff, builder);
                    }

                    #endregion

                    string concatLines = builder.ToString();
                    string concatLinesHash = HashHelper.GetHash(concatLines);

                    newSnapshot.Lines = new List<string>() {concatLinesHash};

                    var areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                    if (areEqual == false)
                    {
                        //maybe the user only inserted lines above... check from bottom

                        newSnapshot.UsedBottomOffset = true;
                        Logger.Info($"created snapshot from old snapshot (from start) and was not equal, " +
                                    $"snapshot: {watchExpression.WatchExpressionFilePath}, " +
                                    $"doc file: {watchExpression.GetDocumentationLocation()}, " +
                                    $"using file bottom offset...");

                        using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                        {
                            buff = new char[reversedLineRangeIndices.Value.GetLength()];

                            sr.BaseStream.Seek(-reversedLineRangeIndices.Value.End, SeekOrigin.End);
                            sr.ReadBlock(buff, 0, (int) reversedLineRangeIndices.Value.GetLength());

                            GetTextFromBuffer(buff, builder);
                        }

                        concatLines = builder.ToString();
                        concatLinesHash = HashHelper.GetHash(concatLines);

                        newSnapshot.Lines = new List<string>() {concatLinesHash};
                    }

                    #endregion
                }
                else
                {
                    List<string> lines = new List<string>();
                    //use plain lines (compressLines == false)

                    #region --- plain lines

                    char[] buff = new char[lineRangeIndices.Value.GetLength()];

                    #region -- first try from top

                    using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                    {
                        sr.BaseStream.Seek(lineRangeIndices.Value.Start, SeekOrigin.Begin);
                        sr.ReadBlock(buff, 0, (int) lineRangeIndices.Value.GetLength());

                        GetLinesFromBuffer(buff, lines, builder);
                    }

                    #endregion

                    newSnapshot.Lines = lines;

                    var areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                    if (areEqual == false)
                    {
                        //maybe the user only inserted lines above... check from bottom

                        newSnapshot.UsedBottomOffset = true;
                        Logger.Info($"created snapshot from old snapshot (from start) and was not equal, " +
                                    $"snapshot: {watchExpression.WatchExpressionFilePath}, " +
                                    $"doc file: {watchExpression.GetDocumentationLocation()}, " +
                                    $"using file bottom offset...");


                        buff = new char[reversedLineRangeIndices.Value.GetLength()];
                        lines = new List<string>();

                        using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                        {
                            sr.BaseStream.Seek(-reversedLineRangeIndices.Value.End, SeekOrigin.End);
                            sr.ReadBlock(buff, 0, (int) reversedLineRangeIndices.Value.GetLength());

                            GetLinesFromBuffer(buff, lines, builder);

                            newSnapshot.Lines = lines;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFilePath}, error (during read): {e.Message}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            //needs to be already set here
            //newSnapshot.Lines = lines;

            return newSnapshot;
        }

        /// <summary>
        /// writes all chars into the string builder except new lines
        /// 
        /// e.g. a \r inside the text is valid
        /// </summary>
        /// <param name="buff"></param>
        /// <param name="builder"></param>
        private static void GetTextFromBuffer(char[] buff, StringBuilder builder)
        {
            //now remove the new line chars...
            builder.Clear();

            for (int i = 0; i < buff.Length - 1; i++)
            {
                var ch = buff[i];
                if (ch == '\n') continue;

                if (ch == '\r' && buff[i + 1] == '\n')
                {
                    i++;
                    continue;
                }

                builder.Append(ch);
            }

            //the original line had e.g. \r\n
            //the new snapshot line has \r as the last char then we added 1 char... so take the \r
            if (buff[buff.Length - 1] == '\n') //if we have \r\n and 
            {
                //ignore because \r\n gets always here
            }
            else
            {
                builder.Append(buff[buff.Length - 1]);
            }
        }


        /// <summary>
        /// returns all lines from the buffer excluding the new line (chars)
        /// </summary>
        /// <param name="buff"></param>
        /// <param name="lines"></param>
        /// <param name="builder"></param>
        private static void GetLinesFromBuffer(char[] buff, List<string> lines, StringBuilder builder)
        {
            //now remove the new line chars... and take lines
            builder.Clear();

            for (int i = 0; i < buff.Length - 1; i++)
            {
                var ch = buff[i];

                if (ch == '\n')
                {
                    lines.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                if (ch == '\r' && buff[i + 1] == '\n')
                {
                    lines.Add(builder.ToString());
                    builder.Clear();
                    i++;
                    continue;
                }

                builder.Append(ch);
            }

            if (buff[buff.Length - 1] == '\n' || buff[buff.Length - 1] == '\r')
            {
                //ignore
                //can be new line if we added 1 char to the line
            }
            else
            {
                builder.Append(buff[buff.Length - 1]);
            }
            lines.Add(builder.ToString());
        }


        public static string GetAbsoluteSnapshotFilePath(string absoluteSnapshotDirectoryPath,
            ISnapshotLike snapshotLike, bool combinedSnapshotFiles)
        {
            string fileName =
                HashHelper.GetHashForFileName(snapshotLike.GetSnapshotFileNameWithoutExtension(combinedSnapshotFiles));
            string filePath = Path.Combine(absoluteSnapshotDirectoryPath, fileName + "." + SnapshotFileExtension);

            return filePath;
        }


        //--- for single snapshots / no combine snapshots

        public static bool SaveSnapshot(string absoluteSnapshotDirectoryPath, Snapshot snapshot,
            bool prettyPrint = false)
        {
            try
            {
                var dirInfo = new DirectoryInfo(absoluteSnapshotDirectoryPath);

                if (dirInfo.Exists == false)
                {
                    Directory.CreateDirectory(dirInfo.FullName);
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not opne/create snapshot dir: {absoluteSnapshotDirectoryPath}, error: {e.Message}");
                return false;
            }

            string fileName = HashHelper.GetHashForFileName(snapshot.GetSnapshotFileNameWithoutExtension(false));
            string filePath = Path.Combine(absoluteSnapshotDirectoryPath, fileName + "." + SnapshotFileExtension);

            try
            {
                string fileContent =
                    JsonConvert.SerializeObject(snapshot, prettyPrint ? Formatting.Indented : Formatting.None);

                File.WriteAllText(filePath, fileContent);
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not serialize or write snapshot to file: {filePath}, snapshot from: {snapshot}, error: {e.Message}");
                return false;
            }

            return true;
        }


        public static bool SnapshotExists(string absoluteSnapshotDirectoryPath, ISnapshotLike snapshotLike)
        {
            string absoluteSnapshotFilePath =
                GetAbsoluteSnapshotFilePath(absoluteSnapshotDirectoryPath, snapshotLike, false);

            return IoHelper.CheckFileExists(absoluteSnapshotFilePath, false);
        }

        public static bool SnapshotCombinedExists(string absoluteSnapshotDirectoryPath, ISnapshotLike snapshotLike,
            out List<Snapshot> readSnapshots)
        {
            readSnapshots = null;

            string absoluteSnapshotFilePath =
                GetAbsoluteSnapshotFilePath(absoluteSnapshotDirectoryPath, snapshotLike, true);


            if (IoHelper.CheckFileExists(absoluteSnapshotFilePath, false) == false) return false;


            //bad performance... if we have a lot of watch expressions that target the same source file
            var snapshots = ReadSnapshots(absoluteSnapshotFilePath);

            if (snapshots == null) return false;

            readSnapshots = snapshots;

            return snapshots.Any(p => p.LineRange == snapshotLike.LineRange);
        }


        public static Snapshot ReadSnapshot(string absoluteSnapshotPath)
        {
            if (IoHelper.CheckFileExists(absoluteSnapshotPath, true) == false) return null;

            Snapshot oldSnapshot = null;

            try
            {
                string serializedSnapshot = File.ReadAllText(absoluteSnapshotPath);

                if (serializedSnapshot.StartsWith("["))
                {
                    Logger.Error($"seems like the saves snapshot at {absoluteSnapshotPath} is a " +
                                 $"combined snapshot but you have not specified the combine option");

                    return null;
                }

                oldSnapshot = JsonConvert.DeserializeObject<Snapshot>(serializedSnapshot);
            }
            catch (Exception e)
            {
                Logger.Error($"could not read snapshot at: {absoluteSnapshotPath}, error: {e.Message}");
                return null;
            }

            return oldSnapshot;
        }


        //for multiple snapshots / combine snapshots

        public static bool SaveSnapshots(string absoluteSnapshotDirectoryPath, List<Snapshot> snapshots,
            bool prettyPrint = false)
        {
            if (snapshots.Count == 0)
            {
                Logger.Warn("tried to save an empty list of snapshots, ignoring");
                return true;
            }

            if (IoHelper.EnsureDirExists(absoluteSnapshotDirectoryPath) == false) return false;


            Snapshot firstSnapshot = snapshots.First();
            string filePath = GetAbsoluteSnapshotFilePath(absoluteSnapshotDirectoryPath, firstSnapshot, true);

            try
            {
                string fileContent =
                    JsonConvert.SerializeObject(snapshots, prettyPrint ? Formatting.Indented : Formatting.None);

                File.WriteAllText(filePath, fileContent);
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not serialize or write snapshots to file: {filePath}, " +
                    $"num snapshots: {snapshots.Count}, first watch expression: {firstSnapshot.ToString()}, error: {e.Message}");
                return false;
            }

            return true;
        }


        public static List<Snapshot> ReadSnapshots(string absoluteSnapshotPath)
        {
            if (IoHelper.CheckFileExists(absoluteSnapshotPath, true) == false) return null;

            List<Snapshot> oldSnapshot = null;

            try
            {
                string serializedSnapshot = File.ReadAllText(absoluteSnapshotPath);

                if (serializedSnapshot.StartsWith("[") == false)
                {
                    Logger.Error($"seems like the saves snapshot at {absoluteSnapshotPath} is not a " +
                                 $"combined snapshot but you have specified the combine option");

                    return null;
                }

                oldSnapshot = JsonConvert.DeserializeObject<List<Snapshot>>(serializedSnapshot);
            }
            catch (Exception e)
            {
                Logger.Error($"could not read snapshot at: {absoluteSnapshotPath}, error: {e.Message}");
                return null;
            }

            return oldSnapshot;
        }
    }
}