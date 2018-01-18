using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using watchCode.model;

namespace watchCode.helpers
{
    public static class SnapshotHelper
    {
        public static string SnapshotFileExtension = "json";

        public static Snapshot CreateSnapshot(string absoluteFileName, string watchExpressionFilePath,
            LineRange? lineRange = null)
        {
            if (lineRange.HasValue && (lineRange.Value.Start <= 0 || lineRange.Value.End <= 0 ||
                                       lineRange.Value.End < lineRange.Value.Start))
            {
                
                //this can happen if the file is e.g. renamed... so jsut a warning
                //the compare will be ok if because the new snapshot will be null
                Logger.Warn(
                    $"could create snapshot for file: {absoluteFileName} because line range was " +
                    $"invalide: start: {lineRange.Value.Start}, end: {lineRange.Value.End}");
                return null;
            }

            try
            {
                var fileInfo = new FileInfo(absoluteFileName);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"could create snapshot for file: {absoluteFileName} because file not exists");
                    return null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could not create file info for file: {absoluteFileName}, error: {e.Message}");
                return null;
            }

            //file exists


            List<string> lines = new List<string>();

            int count = 1;
            string line;

            try
            {
                using (var sr = new StreamReader(File.Open(absoluteFileName, FileMode.Open)))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (lineRange == null)
                        {
                            //add all files
                            lines.Add(line);
                        }
                        else
                        {
                            if (count >= lineRange.Value.Start && count <= lineRange.Value.End) lines.Add(line);

                            if (count > lineRange.Value.End) break;
                        }
                        

                        count++;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not create snapshot for file: {absoluteFileName}, error (during read): {e.Message}");
                return null;
            }

            return new Snapshot(watchExpressionFilePath, lineRange, lines);
        }


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

            string fileName = MD5Helper.GetHash(snapshot.GetIdentifier());
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
            string absoluteSnapshotFilePath = GetAbsoluteSnapshotFilePath(absoluteSnapshotDirectoryPath, snapshotLike);

            try
            {
                var fileInfo = new FileInfo(absoluteSnapshotFilePath);

                if (fileInfo.Exists)
                {
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"could not check if snapshot: {absoluteSnapshotFilePath} exists, error: {e.Message}");
            }

            return false;
        }


        public static Snapshot ReadSnapshot(string absoluteSnapshotPath)
        {
            try
            {
                var fileInfo = new FileInfo(absoluteSnapshotPath);

                if (fileInfo.Exists == false)
                {
                    Logger.Error($"snapshot does not exists, path: {absoluteSnapshotPath}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could access snapshot at: {absoluteSnapshotPath}, error: {e.Message}");
                return null;
            }

            Snapshot oldSnapshot = null;

            try
            {
                string serializedSnapshot = File.ReadAllText(absoluteSnapshotPath);
                oldSnapshot = JsonConvert.DeserializeObject<Snapshot>(serializedSnapshot);

            }
            catch (Exception e)
            {
                Logger.Error($"could not read snapshot at: {absoluteSnapshotPath}, error: {e.Message}");
                return null;
            }
            
            return oldSnapshot;
        }

        public static string GetAbsoluteSnapshotFilePath(string absoluteSnapshotDirectoryPath,
            ISnapshotLike snapshotLike)
        {
            string fileName = MD5Helper.GetHash(snapshotLike.GetIdentifier());
            string filePath = Path.Combine(absoluteSnapshotDirectoryPath, fileName + "." + SnapshotFileExtension);

            return filePath;
        }
    }
}