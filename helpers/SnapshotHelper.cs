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
            bool compressLines)
        {
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

            FileInfo fileInfo;

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

            //file exists

            List<string> lines = new List<string>();

            int count = 1;
            string line;

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
                                if (count >= watchExpression.LineRange.Value.Start &&
                                    count <= watchExpression.LineRange.Value.End)
                                    builder.Append(line);

                                if (count > watchExpression.LineRange.Value.End) break;

                                count++;
                            }
                        }

                        string concatLines = builder.ToString();
                        lines.Add(HashHelper.GetHash(concatLines));
                    }

                    #endregion
                }
                else
                {
                    //use plain lines (compressLines == false)

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
                                if (count >= watchExpression.LineRange.Value.Start &&
                                    count <= watchExpression.LineRange.Value.End) lines.Add(line);

                                if (count > watchExpression.LineRange.Value.End) break;
                            }
                            count++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFilePath}, error (during read): {e.Message}, " +
                    $"at doc file: {watchExpression.GetDocumentationLocation()}");
                return null;
            }

            return new Snapshot(watchExpression.WatchExpressionFilePath, watchExpression.LineRange, lines);
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