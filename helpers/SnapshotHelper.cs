using System;
using System.Collections.Generic;
using System.Diagnostics;
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


        private static Snapshot CheckAndCorrectSnapshot(WatchExpression watchExpression, Snapshot snapshot)
        {
            if (snapshot.LineRange != null)
            {
                if (snapshot.LineRange.Start > snapshot.TotalLinesInFile)
                {
                    Logger.Warn($"watch expression at: {watchExpression.GetDocumentationLocation()} has " +
                                $"too large line range (start), watched file: {watchExpression.WatchExpressionFilePath} " +
                                $"has actually only {snapshot.TotalLinesInFile} lines in total...");

                    Logger.Warn($"...using total lines in file for end range value...");
                    snapshot.LineRange.Start = snapshot.TotalLinesInFile;
                }

                if (snapshot.LineRange.End > snapshot.TotalLinesInFile)
                {
                    Logger.Warn($"watch expression at: {watchExpression.GetDocumentationLocation()} has " +
                                $"too large line range (end), watched file: {watchExpression.WatchExpressionFilePath} " +
                                $"has actually only {snapshot.TotalLinesInFile} lines in total...");

                    Logger.Warn($"...using total lines in file for end range value...");
                    snapshot.LineRange.End = snapshot.TotalLinesInFile;
                }


                if (snapshot.ReversedLineRange == null)
                {
                    Logger.Warn(
                        $"reverse line range cannot be null if LineRange is set, " +
                        $"doc file: {watchExpression.GetDocumentationLocation()}");
                    return null;
                }

                if (snapshot.LineRange.Start <= 0)
                {
                    Logger.Warn($"watch expression start line range at: {watchExpression.GetDocumentationLocation()} " +
                                $"has < 1 value which is invalid, must be >= 1 ... using 1 instead");
                    snapshot.LineRange.Start = 1;
                }

                if (snapshot.LineRange.End <= 0)
                {
                    Logger.Warn($"watch expression end line range at: {watchExpression.GetDocumentationLocation()} " +
                                $"has < 1 value which is invalid, must be >= 1");
                    return null;
                }

                if (snapshot.LineRange.End < snapshot.LineRange.Start)
                {
                    Logger.Warn($"watch expression line range at: {watchExpression.GetDocumentationLocation()} " +
                                $"has a larger end line number than start line number");
                    return null;
                }

                if (snapshot.LineRange.GetLength() != snapshot.ReversedLineRange.GetLength())
                {
                    Logger.Warn(
                        $"watch expression line range and reverse line range have not equal length, " +
                        $"doc file: {watchExpression.GetDocumentationLocation()}");
                    return null;
                }

                if (snapshot.Lines.Count != snapshot.LineRange.GetLength())
                {
                    Logger.Warn(
                        $"watch expression line range and saved lines (length) are not equal, " +
                        $"doc file: {watchExpression.GetDocumentationLocation()}");
                    return null;
                }
            }


            return snapshot;
        }

        //TODO rework logg output
        /// <summary>
        /// </summary>
        /// <param name="absoluteFilePath"></param>
        /// <param name="watchExpression"></param>
        /// <returns></returns>
        public static Snapshot CreateSnapshot(string absoluteFilePath, WatchExpression watchExpression)
        {
            FileInfo fileInfo;

            #region --- some checks

            if (watchExpression.LineRange != null &&
                (watchExpression.LineRange.Start <= 0 || watchExpression.LineRange.End <= 0 ||
                 watchExpression.LineRange.End < watchExpression.LineRange.Start))
            {
                //this can happen if the file is e.g. renamed... so jsut a warning
                //the compare will be ok if because the new snapshot will be null
                Logger.Warn(
                    $"could not create snapshot for file: {absoluteFilePath} because line range was " +
                    $"invalide: start: {watchExpression.LineRange.Start}, end: {watchExpression.LineRange.End}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }


            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error(
                        $"could not create snapshot for file: {absoluteFilePath} because file does not exists, " +
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

            int linesCount = 1;
            string line;

            LineRange reversedLineRange = null;
            Snapshot newSnapshot;

            try
            {
                if (watchExpression.LineRange == null)
                {
                    //only watch if the file change somehow
                    string fileHash = HashHelper.GetHashForFile(fileInfo);
                    newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath, fileHash);
                }
                else
                {
                    using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                    {
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (linesCount >= watchExpression.LineRange.Start &&
                                linesCount <= watchExpression.LineRange.End) lines.Add(line);


                            linesCount++;
                        }
                    }

                    reversedLineRange = new LineRange(
                        linesCount - watchExpression.LineRange.End,
                        linesCount - watchExpression.LineRange.Start
                    );

                    newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath,
                        watchExpression.LineRange,
                        reversedLineRange,
                        linesCount,
                        lines);
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFilePath}, error (during read): {e.Message}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }


            return CheckAndCorrectSnapshot(watchExpression, newSnapshot);
        }


        //this also checks the bottom offset lines...
        //actually
        public static Snapshot CreateSnapshotBasedOnOldSnapshot(string absoluteFilePath,
            WatchExpression watchExpression, Snapshot oldSnapshot, out bool snapshotsWereEqual)
        {
            var stopwatch = new Stopwatch();

            FileInfo fileInfo;
            snapshotsWereEqual = false;

            #region --- some checks

            if (oldSnapshot == null)
            {
                Logger.Error($"tried to create a snapshot based on a null snapshot, call " +
                             $"{nameof(SnapshotHelper.CreateSnapshot)} instead, skipping");
                return null;
            }

            if (watchExpression.LineRange == null)
            {
                Logger.Error($"tried to create a snapshot based on an old one but for the whole file, call " +
                             $"{nameof(SnapshotHelper.CreateSnapshot)} instead, skipping");
                return null;
            }

            if (watchExpression.LineRange.Start <= 0 || watchExpression.LineRange.End <= 0 ||
                watchExpression.LineRange.End < watchExpression.LineRange.Start)
            {
                //this can happen if the file is e.g. renamed... so jsut a warning
                //the compare will be ok if because the new snapshot will be null
                Logger.Warn(
                    $"could not create snapshot for file: {absoluteFilePath} because line range was " +
                    $"invalide: start: {watchExpression.LineRange.Start}, end: {watchExpression.LineRange.End}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            oldSnapshot = CheckAndCorrectSnapshot(watchExpression, oldSnapshot);

            if (oldSnapshot == null) return null;


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
                    Logger.Error(
                        $"could not create snapshot for file: {absoluteFilePath} because file does not exists, " +
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


            stopwatch.Start();

            //file exists

            string line;
            int linesCount = 1;

//            var newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath, watchExpression.LineRange,
//                new List<string>());
            List<string> lines = new List<string>();
            Snapshot newSnapshot;

            try
            {
                //use plain lines (compressLines == false)
                //var lines = new string[watchExpression.LineRange.GetLength()];

                #region --- normal top down

                using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (linesCount >= watchExpression.LineRange.Start &&
                            linesCount <= watchExpression.LineRange.End)
                        {
                            lines.Add(line);
                        }
                        linesCount++;
                    }
                }

                newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath, oldSnapshot.LineRange,
                    oldSnapshot.ReversedLineRange, linesCount, lines);

                #endregion

                var areEqual = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                if (areEqual)
                {
                    snapshotsWereEqual = true;

                    stopwatch.Stop();
                    Logger.Info($"snapshot created from old one (found lines, used top offset) for doc " +
                                $"file: {watchExpression.GetDocumentationLocation()}, " +
                                $"source file: {watchExpression.GetSourceFileLocation()} " +
                                $"(in {StopWatchHelper.GetElapsedTime(stopwatch)})");

                    return newSnapshot;
                }


                #region --- check bottom to top (reverse line range)

                //maybe the user only inserted lines above... check from bottom

                //empty list but keep count
                //if we have few lines now and we don't get the full range (user deleted lines
                //and now the line range is outside of the file line range) then we would use some old
                //lines from thhe top down pass through
                lines = lines.Select(p => (string) null).ToList();

                int count = 1;
                {
                    var sr = new ReverseLineReader(absoluteFilePath);

                    foreach (var _line in sr)
                    {
                        if (count >= oldSnapshot.ReversedLineRange.Start &&
                            count <= oldSnapshot.ReversedLineRange.End)
                        {
                            lines[lines.Count - (count - oldSnapshot.ReversedLineRange.Start) - 1] =
                                _line;
                        }

                        //we already have the total lines count so we can stop early
                        if (count > oldSnapshot.ReversedLineRange.End) break;

                        count++;
                    }
                }

                newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath, oldSnapshot.LineRange,
                    oldSnapshot.ReversedLineRange, linesCount, lines);

                newSnapshot.TriedBottomOffset = true;

                #endregion


                var areEqualFromBottom = SnapshotWrapperHelper.AreSnapshotsEqual(oldSnapshot, newSnapshot);

                if (areEqualFromBottom)
                {
                    Logger.Info($"snapshot created from old one (found lines, used bottom offset) for doc " +
                                $"file: {watchExpression.GetDocumentationLocation()}, " +
                                $"source file: {watchExpression.GetSourceFileLocation()} " +
                                $"(in {StopWatchHelper.GetElapsedTime(stopwatch)})");
                    snapshotsWereEqual = true;
                    return newSnapshot;
                }

                #region --- search through the whole file and try find the lines

                //well the user could have inserted lines before and after the watched section/code...
                //try to find the old lines (from the old snapshot) in the file
                int equalLine = 0;
                int newStartLine = 0;

                lines.Clear();

                count = 1;
                using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line == oldSnapshot.Lines[equalLine])
                        {
                            if (equalLine == 0) newStartLine = count;

                            lines.Add(line);
                            equalLine++; //found a subsequent equal line

                            if (equalLine == oldSnapshot.Lines.Count)
                            {
                                //found all
                                break;
                            }
                        }
                        else
                        {
                            if (equalLine > 0) //we already found equal lines
                            {
                                //but now this line don't match so not ALL lines match
                                equalLine = 0;
                                lines.Clear();
                            }
                        }
                        count++;
                    }
                }

                if (equalLine == oldSnapshot.Lines.Count)
                {
                    //found all lines...
                    //store the new line range (only top because bottom will be automatically updated on doc update

                    newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath,
                        new LineRange(newStartLine, newStartLine + equalLine),
                        oldSnapshot.ReversedLineRange, linesCount,
                        oldSnapshot.Lines.ToList() //lines are equal
                    );

                    Logger.Info($"snapshot created from old one (found lines, used search) for doc " +
                                $"file: {watchExpression.GetDocumentationLocation()}, " +
                                $"source file: {watchExpression.GetSourceFileLocation()} " +
                                $"(in {StopWatchHelper.GetElapsedTime(stopwatch)})");
                    
                    snapshotsWereEqual = true;
                }
                else
                {
                    newSnapshot = new Snapshot(watchExpression.WatchExpressionFilePath,
                        new LineRange(newStartLine, oldSnapshot.LineRange.GetLength()),
                        oldSnapshot.ReversedLineRange, linesCount,
                        lines
                    );
                    
                    Logger.Info($"snapshot created from old one (lines were not found) for doc " +
                                $"file: {watchExpression.GetDocumentationLocation()}, " +
                                $"source file: {watchExpression.GetSourceFileLocation()} " +
                                $"(in {StopWatchHelper.GetElapsedTime(stopwatch)})");
                    
                    snapshotsWereEqual = false;
                }

                //newSnapshot.TriedBottomOffset is false because we created a new snapshot this is what we want
                newSnapshot.TriedSearchFileOffset = true;

                #endregion
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for doc file: {watchExpression.GetDocumentationLocation()}, " +
                    $"source file: {watchExpression.GetSourceFileLocation()},error (during read): {e.Message}");
                return null;
            }

            return newSnapshot;
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
            WatchExpression watchExpression, bool prettyPrint = false)
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
                    $"could not opne/create snapshot dir: {absoluteSnapshotDirectoryPath}, " +
                    $"doc file: {watchExpression.GetDocumentationLocation()}, " +
                    $"source file: {watchExpression.GetSourceFileLocation()}, error: {e.Message}");
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
                    $"could not serialize or write snapshot to file: {filePath}, " +
                    $"doc file: {watchExpression.GetDocumentationLocation()}, " +
                    $"source file: {watchExpression.GetSourceFileLocation()}, error: {e.Message}");
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
