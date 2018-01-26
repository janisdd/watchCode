using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace watchCode.helpers
{
    public static class FileIteratorHelper
    {
        public static string HiddenFileStartWithString = ".";

        public static List<FileInfo> GetAllFiles(List<string> files, List<string> dirs, List<string> whiteListFilterByExtensions,
            bool recursiveCheckDirs, List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore,
            bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>(files.Count);

            fileInfos.AddRange(GetAllFiles(files, whiteListFilterByExtensions, absoluteFilePathsToIgnore,
                absoluteDirPathsToIgnore, ignoreHiddenFiles));

            fileInfos.AddRange(GetAllFilesInDirs(dirs, whiteListFilterByExtensions, recursiveCheckDirs,
                absoluteFilePathsToIgnore, absoluteDirPathsToIgnore, ignoreHiddenFiles));

            return fileInfos;
        }

        private static List<FileInfo> GetAllFilesInDirs(List<string> dirs, List<string> filterByExtensions,
            bool recursiveCheckDirs, List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore,
            bool ignoreHiddenFiles)
        {
            var dirInfos = new List<DirectoryInfo>();

            for (int i = 0; i < dirs.Count; i++)
            {
                string absoluteDirPath = Path.Combine(DynamicConfig.AbsoluteRootDirPath, dirs[i]);


                try
                {
                    DirectoryInfo dirInfo = null;
                    dirInfo = new DirectoryInfo(absoluteDirPath);

                    if (!dirInfo.Exists)
                    {
                        Logger.Info($"directory not exists: {absoluteDirPath}, ignoring");
                        continue;
                    }

                    dirInfos.Add(dirInfo);
                }
                catch (Exception e)
                {
                    Logger.Warn(
                        $"could not get directory info for file: {absoluteDirPath}, error: {e.Message}, ignoring");
                }
            }

            return GetAllFilesInDirs(dirInfos, filterByExtensions, recursiveCheckDirs, absoluteFilePathsToIgnore,
                absoluteDirPathsToIgnore, ignoreHiddenFiles);
        }


        private static List<FileInfo> GetAllFilesInDirs(IEnumerable<DirectoryInfo> dirs,
            List<string> filterByExtensions, bool recursiveCheckDirs, List<string> absoluteFilePathsToIgnore,
            List<string> absoluteDirPathsToIgnore, bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>();

            foreach (var directoryInfo in dirs)
            {
                fileInfos.AddRange(GetAllFiles(directoryInfo.GetFiles(), filterByExtensions, absoluteFilePathsToIgnore,
                    absoluteDirPathsToIgnore, ignoreHiddenFiles));

                if (recursiveCheckDirs == false) continue;

                var subDirs = directoryInfo.GetDirectories();
                var subDirFiles = GetAllFilesInDirs(subDirs, filterByExtensions, recursiveCheckDirs,
                    absoluteFilePathsToIgnore, absoluteDirPathsToIgnore, ignoreHiddenFiles);
                fileInfos.AddRange(subDirFiles);
            }

            return fileInfos;
        }

        private static List<FileInfo> GetAllFiles(List<string> files, List<string> whiteListFilterByExtensions,
            List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore, bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                string absoluteFilePath = Path.Combine(DynamicConfig.AbsoluteRootDirPath, files[i]);

                try
                {
                    FileInfo fileInfo = new FileInfo(absoluteFilePath);

                    if (!fileInfo.Exists)
                    {
                        Logger.Info($"file does not exist: {fileInfo.FullName}");
                        continue;
                    }

                    //disallow empty extension 
                    if (fileInfo.Extension.Length == 0 ||
                        whiteListFilterByExtensions.Contains(fileInfo.Extension.Substring(1)) == false)
                    {
                        Logger.Info($"ignoring file because of extension filter: {fileInfo.FullName}");
                        continue;
                    }

                    if (fileInfo.Name.StartsWith(HiddenFileStartWithString))
                    {
                        Logger.Info($"ignoring file because hidden: {fileInfo.FullName}");
                        continue;
                    }

                    fileInfos.Add(fileInfo);
                }
                catch (Exception e)
                {
                    Logger.Warn($"could not get file info for file: {absoluteFilePath}, error: {e.Message}");
                }
            }
            return GetAllFiles(fileInfos, whiteListFilterByExtensions, absoluteFilePathsToIgnore, absoluteDirPathsToIgnore,
                ignoreHiddenFiles);
        }


        private static List<FileInfo> GetAllFiles(IEnumerable<FileInfo> files, List<string> whiteListFilterByExtensions,
            List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore, bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>();

            foreach (var fileInfo in files)
            {
                if (!fileInfo.Exists)
                {
                    Logger.Info($"file does not exist: {fileInfo.FullName}");
                    continue;
                }

                if (absoluteFilePathsToIgnore.Contains(fileInfo.FullName))
                {
                    Logger.Info($"ignoring file because specified in ignore list: {fileInfo.FullName}");
                    continue;
                }

                if (absoluteDirPathsToIgnore.Any(p => fileInfo.FullName.StartsWith(p)))
                {
                    Logger.Info($"ignoring file because dir specified in ignore list: {fileInfo.FullName}");
                    continue;
                }

                //check extension

                //disallow empty extension 
                if (fileInfo.Extension.Length == 0 ||
                    whiteListFilterByExtensions.Contains(fileInfo.Extension.Substring(1)) == false)
                {
                    Logger.Info($"ignoring file because of extension filter: {fileInfo.FullName}");
                    continue;
                }

                if (fileInfo.Name.StartsWith(HiddenFileStartWithString))
                {
                    Logger.Info($"ignoring file because hidden: {fileInfo.FullName}");
                    continue;
                }

                fileInfos.Add(fileInfo);
                Logger.Info($"added doc file file {fileInfo.FullName}");
            }

            return fileInfos;
        }
    }
}
