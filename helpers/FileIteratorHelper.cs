﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace watchCode.helpers
{
    public static class FileIteratorHelper
    {
        public static List<FileInfo> GetAllFiles(List<string> files, List<string> dirs, List<string> filterByExtensions,
            bool recursiveCheckDirs, List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore)
        {
            List<FileInfo> fileInfos = new List<FileInfo>(files.Count);

            fileInfos.AddRange(GetAllFiles(files, filterByExtensions, absoluteFilePathsToIgnore,
                absoluteDirPathsToIgnore));

            fileInfos.AddRange(GetAllFilesInDirs(dirs, filterByExtensions, recursiveCheckDirs,
                absoluteFilePathsToIgnore, absoluteDirPathsToIgnore));

            return fileInfos;
        }

        private static List<FileInfo> GetAllFilesInDirs(List<string> dirs, List<string> filterByExtensions,
            bool recursiveCheckDirs, List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore)
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
                        Logger.Warn($"directory not exists: {absoluteDirPath}, ignoring");
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
                absoluteDirPathsToIgnore);
        }


        private static List<FileInfo> GetAllFilesInDirs(IEnumerable<DirectoryInfo> dirs,
            List<string> filterByExtensions, bool recursiveCheckDirs, List<string> absoluteFilePathsToIgnore,
            List<string> absoluteDirPathsToIgnore)
        {
            List<FileInfo> fileInfos = new List<FileInfo>();

            foreach (var directoryInfo in dirs)
            {
                fileInfos.AddRange(GetAllFiles(directoryInfo.GetFiles(), filterByExtensions, absoluteFilePathsToIgnore,
                    absoluteDirPathsToIgnore));

                if (recursiveCheckDirs == false) continue;

                var subDirs = directoryInfo.GetDirectories();
                var subDirFiles = GetAllFilesInDirs(subDirs, filterByExtensions, recursiveCheckDirs,
                    absoluteFilePathsToIgnore, absoluteDirPathsToIgnore);
                fileInfos.AddRange(subDirFiles);
            }

            return fileInfos;
        }

        private static List<FileInfo> GetAllFiles(List<string> files, List<string> filterByExtensions,
            List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore)
        {
            List<FileInfo> fileInfos = new List<FileInfo>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                string absoluteFilePath = Path.Combine(DynamicConfig.AbsoluteRootDirPath, files[i]);

                try
                {
                    FileInfo fileInfo = new FileInfo(absoluteFilePath);
                    fileInfos.Add(fileInfo);
                }
                catch (Exception e)
                {
                    Logger.Warn($"could not get file info for file: {absoluteFilePath}, error: {e.Message}");
                }
            }
            return GetAllFiles(fileInfos, filterByExtensions, absoluteFilePathsToIgnore, absoluteDirPathsToIgnore);
        }


        private static List<FileInfo> GetAllFiles(IEnumerable<FileInfo> files, List<string> filterByExtensions,
            List<string> absoluteFilePathsToIgnore, List<string> absoluteDirPathsToIgnore)
        {
            List<FileInfo> fileInfos = new List<FileInfo>();

            foreach (var fileInfo in files)
            {
                if (!fileInfo.Exists)
                {
                    Logger.Warn($"file not exists: {fileInfo.FullName}");
                    continue;
                }

                if (absoluteFilePathsToIgnore.Contains(fileInfo.FullName))
                {
                    Logger.Warn($"ignoring file because specified: {fileInfo.FullName}");
                    continue;
                }

                if (absoluteDirPathsToIgnore.Any(p => fileInfo.FullName.StartsWith(p)))
                {
                    Logger.Warn($"ignoring file because specified (directory): {fileInfo.FullName}");
                    continue;
                }

                //check extension

                //disallow empty extension 
                if (fileInfo.Extension.Length > 0 && filterByExtensions.Contains(fileInfo.Extension.Substring(1)))
                {
                    fileInfos.Add(fileInfo);
                    Logger.Info($"added file {fileInfo.FullName}");
                    continue;
                }


                Logger.Info($"ignoring file because of extension filter: {fileInfo.FullName}");
            }

            return fileInfos;
        }
    }
}