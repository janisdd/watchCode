using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using watchCode.model;
using watchCode.model.snapshots;

namespace watchCode.helpers
{
    public static class SnapshotHelper
    {
        public static string SnapshotFileExtension = "json";


        private static Snapshot CheckAndCorrectRangeSnapshot(WatchExpression watchExpression,
            Snapshot snapshot)
        {
            if (snapshot.LineRange == null)
            {
                Logger.Error($"snapshots line range was null, watch expression: {watchExpression.GetFullIdentifier()}");
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

            if (snapshot.Lines.Count != snapshot.LineRange.GetLength())
            {
                Logger.Warn(
                    $"watch expression line range and saved lines (length) are not equal, " +
                    $"doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            return snapshot;
        }

        /// <summary>
        /// </summary>
        /// <param name="absoluteFilePath"></param>
        /// <param name="watchExpression"></param>
        /// <returns></returns>
        public static Snapshot CreateSnapshot(string absoluteFilePath, WatchExpression watchExpression)
        {
            if (watchExpression.LineRange == null)
            {
                string message = "file snapshots are not supported (yet)!";
                Logger.Error(message);
                throw new NotImplementedException(message);
            }

            return CreateRangeSnapshot(absoluteFilePath, watchExpression);
        }


        private static Snapshot CreateRangeSnapshot(string absoluteFilePath, WatchExpression watchExpression)
        {
            FileInfo fileInfo;

            List<string> lines = new List<string>();
            string line;
            int linesCount = 1;
            Snapshot newSnapshot;

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
                        $"could not create snapshot for file: {absoluteFilePath} because file does not exist, " +
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


            try
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

                newSnapshot = new Snapshot(watchExpression.SourceFilePath, watchExpression.LineRange, lines);
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFilePath}, error (during read): {e.Message}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            return CheckAndCorrectRangeSnapshot(watchExpression, newSnapshot);
        }

        //this also checks the bottom offset lines...
        //actually
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="absoluteFilePath"></param>
        /// <param name="watchExpression"></param>
        /// <param name="oldSnapshot"></param>
        /// <param name="snapshotsWereEqual">true: snapshots were equal (check needToUpdateRange too), false: could not find old lines</param>
        /// <param name="needToUpdateRange">true: range needs to be updated, false: not</param>
        /// <returns></returns>
        public static Snapshot CreateSnapshotBasedOnOldSnapshot(string absoluteFilePath,
            WatchExpression watchExpression, Snapshot oldSnapshot, out bool snapshotsWereEqual, out bool needToUpdateRange)
        {
            var stopwatch = new Stopwatch();

            FileInfo fileInfo;
            snapshotsWereEqual = false;
            needToUpdateRange = false;

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

            oldSnapshot = CheckAndCorrectRangeSnapshot(watchExpression, oldSnapshot);

            if (oldSnapshot == null) return null;

            try
            {
                fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error(
                        $"could not create snapshot for file: {absoluteFilePath} because file does not exist, " +
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

            //in case we cannot find the original lines use the top to bottom offset to display the latest lines
            List<string> topDownlines = new List<string>();

            Snapshot newSnapshot;

            try
            {
                #region --- normal top down

                using (var sr = new StreamReader(File.Open(absoluteFilePath, FileMode.Open)))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (linesCount >= watchExpression.LineRange.Start &&
                            linesCount <= watchExpression.LineRange.End)
                        {
                            lines.Add(line);
                            topDownlines.Add(line);
                        }

                        linesCount++;
                    }
                }

                newSnapshot = new Snapshot(watchExpression.SourceFilePath, oldSnapshot.LineRange, lines);

                #endregion

                var areEqual = AreSnapshotsEqual(oldSnapshot, newSnapshot);

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


                needToUpdateRange = true;
                
                #region --- search through the whole file and try find the lines

                //well the user could have inserted lines before and after the watched section/code...
                //try to find the old lines (from the old snapshot) in the file
                int equalLine = 0;
                int newStartLine = 0;

                lines.Clear();

                int count = 1;
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

                    newSnapshot = new Snapshot(watchExpression.SourceFilePath,
                        new LineRange(newStartLine, newStartLine + equalLine - 1), //-1 because eq is like count...
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
                    newSnapshot = new Snapshot(watchExpression.SourceFilePath,
                        new LineRange(newStartLine, oldSnapshot.LineRange.GetLength()),
                        topDownlines
                    );

                    Logger.Info($"snapshot created from old one (lines were not found) for doc " +
                                $"file: {watchExpression.GetDocumentationLocation()}, " +
                                $"source file: {watchExpression.GetSourceFileLocation()} " +
                                $"(in {StopWatchHelper.GetElapsedTime(stopwatch)})");

                    snapshotsWereEqual = false;
                }

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
            ISnapshotLike snapshotLike)
        {
            string fileName =
                HashHelper.GetHashForFileName(snapshotLike.GetSnapshotFileNameWithoutExtension());
            string filePath = Path.Combine(absoluteSnapshotDirectoryPath, fileName + "." + SnapshotFileExtension);

            return filePath;
        }


        //--- for single snapshots / no combine snapshots

        public static bool SaveSnapshot(string absoluteSnapshotDirectoryPath, Snapshot snapshot,
            WatchExpression watchExpression, bool prettyPrint = true)
        {

            if (snapshot == null) return false;
            
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

            string fileName = HashHelper.GetHashForFileName(snapshot.GetSnapshotFileNameWithoutExtension());

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
                GetAbsoluteSnapshotFilePath(absoluteSnapshotDirectoryPath, snapshotLike);

            return IoHelper.CheckFileExists(absoluteSnapshotFilePath, false);
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




        public static bool AreSnapshotsEqual(Snapshot oldSnapshot, Snapshot newSnapshot,
            bool compareMetaData = true)
        {
            if (oldSnapshot == null || newSnapshot == null) return false;

            if (compareMetaData)
            {
                if (oldSnapshot.SourceFilePath != newSnapshot.SourceFilePath) return false;

                if (oldSnapshot.LineRange != newSnapshot.LineRange) return false;
            }

            //this cannot happen if the watch expression not change
            //if the watch expression was updaten then the file name should have changed

            if (oldSnapshot.Lines.Count != newSnapshot.Lines.Count) return false; //check this for more common cases...

            for (int i = 0; i < oldSnapshot.Lines.Count; i++)
            {
                var oldLine = oldSnapshot.Lines[i];
                var newLine = newSnapshot.Lines[i];

                if (oldLine != newLine) return false;
            }

            return true;
        }
    }
}
