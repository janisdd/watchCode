using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace watchCode.helpers
{
    public static class FileIteratorHelper
    {
        public static string HiddenFileStartWithString = ".";

        public static List<FileInfo> GetAllFiles(string absolutePathRoot, List<string> filesRelativePath,
            List<string> dirsRelativePath,
            List<string> whiteListFilterByExtensions,
            bool recursiveCheckDirs, List<string> fileNamesToIgnore, List<string> dirNamesToIgnore,
            bool ignoreHiddenFiles)
        {
            DirectoryInfo rootDirInfo;
            try
            {
                rootDirInfo = new DirectoryInfo(absolutePathRoot);

                if (rootDirInfo.Exists == false)
                {
                    Logger.Info($"directory does not exist: {absolutePathRoot}, ignoring");
                    return new List<FileInfo>();
                }
            }
            catch (Exception e)
            {
                Logger.Warn(
                    $"could not get directory info: {absolutePathRoot}, error: {e.Message}, ignoring");
                return new List<FileInfo>();
            }

            List<FileInfo> fileInfos = new List<FileInfo>(filesRelativePath.Count);

            fileInfos.AddRange(GetAllFiles(absolutePathRoot, filesRelativePath, whiteListFilterByExtensions,
                fileNamesToIgnore,
                ignoreHiddenFiles));


            fileInfos.AddRange(GetAllFilesInDirs(rootDirInfo, dirsRelativePath,
                whiteListFilterByExtensions, recursiveCheckDirs,
                fileNamesToIgnore, dirNamesToIgnore, ignoreHiddenFiles));

            return fileInfos;
        }

        private static List<FileInfo> GetAllFilesInDirs(DirectoryInfo rootDirInfo, List<string> dirs,
            List<string> filterByExtensions,
            bool recursiveCheckDirs, List<string> fileNamesToIgnore, List<string> dirNamesToIgnore,
            bool ignoreHiddenFiles)
        {
            var dirInfos = new List<DirectoryInfo>();

            for (int i = 0; i < dirs.Count; i++)
            {
                string absoluteDirPath = Path.Combine(rootDirInfo.FullName, dirs[i]);


                try
                {
                    DirectoryInfo dirInfo = null;
                    dirInfo = new DirectoryInfo(absoluteDirPath);

                    if (!dirInfo.Exists)
                    {
                        Logger.Info($"directory does not exist: {absoluteDirPath}, ignoring");
                        continue;
                    }

                    if (dirNamesToIgnore.Contains(dirInfo.Name))
                    {
                        Logger.Info($"ignoring dir because dir name was specified in ignore list: {dirInfo.Name}");
                        continue;
                    }

                    dirInfos.Add(dirInfo);
                }
                catch (Exception e)
                {
                    Logger.Warn(
                        $"could not get directory info: {absoluteDirPath}, error: {e.Message}, ignoring");
                }
            }

            return GetAllFilesInDirs(rootDirInfo, dirInfos, filterByExtensions, recursiveCheckDirs, fileNamesToIgnore,
                dirNamesToIgnore, ignoreHiddenFiles);
        }


        private static List<FileInfo> GetAllFilesInDirs(DirectoryInfo rootDirInfo, IEnumerable<DirectoryInfo> dirs,
            List<string> filterByExtensions, bool recursiveCheckDirs, List<string> fileNamesToIgnore,
            List<string> dirNamesToIgnore,
            bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>();

            foreach (var directoryInfo in dirs)
            {
                if ((directoryInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) {
                    
                    Logger.Info($"ignoring dir because hidden: {directoryInfo.Name}");
                    continue;
                }
                
                string simpleRelativeToRootPath = ToNormalizedDirFullName(directoryInfo)
                    .Replace(ToNormalizedDirFullName(rootDirInfo), "");

                string normalisedDirName = ToNormalizedDirName(simpleRelativeToRootPath);

                if (dirNamesToIgnore.Contains(normalisedDirName))
                {
                    Logger.Info($"ignoring dir because dir name was specified in ignore list: {simpleRelativeToRootPath}");
                    continue;
                }


                fileInfos.AddRange(FilterFiles(directoryInfo.GetFiles(), filterByExtensions, fileNamesToIgnore,
                    ignoreHiddenFiles));

                if (recursiveCheckDirs == false) continue;

                var subDirs = directoryInfo.GetDirectories();
                var subDirFiles = GetAllFilesInDirs(rootDirInfo, subDirs, filterByExtensions, recursiveCheckDirs,
                    fileNamesToIgnore, dirNamesToIgnore, ignoreHiddenFiles);
                fileInfos.AddRange(subDirFiles);
            }

            return fileInfos;
        }

        private static List<FileInfo> GetAllFiles(string absolutePathRoot, List<string> files,
            List<string> whiteListFilterByExtensions,
            List<string> fileNamesToIgnore, bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                string absoluteFilePath = Path.Combine(absolutePathRoot, files[i]);

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
            return FilterFiles(fileInfos, whiteListFilterByExtensions, fileNamesToIgnore,
                ignoreHiddenFiles);
        }


        private static List<FileInfo> FilterFiles(IEnumerable<FileInfo> files, List<string> whiteListFilterByExtensions,
            List<string> fileNamesToIgnore, bool ignoreHiddenFiles)
        {
            List<FileInfo> fileInfos = new List<FileInfo>();

            foreach (var fileInfo in files)
            {
                if (!fileInfo.Exists)
                {
                    Logger.Info($"file does not exist: {fileInfo.FullName}");
                    continue;
                }

                if (fileNamesToIgnore.Contains(fileInfo.Name))
                {
                    Logger.Info($"ignoring file because specified in ignore list: {fileInfo.Name}");
                    continue;
                }

//                if (dirNamesToIgnore.Any(p => fileInfo.FullName.StartsWith(p)))
//                {
//                    Logger.Info($"ignoring file because dir specified in ignore list: {fileInfo.FullName}");
//                    continue;
//                }

                //check extension

                //disallow empty extension 
                if (fileInfo.Extension.Length == 0 ||
                    whiteListFilterByExtensions.Contains(fileInfo.Extension.Substring(1)) == false)
                {
                    Logger.Info($"ignoring file because of extension filter: {fileInfo.FullName}");
                    continue;
                }

                if (ignoreHiddenFiles && fileInfo.Name.StartsWith(HiddenFileStartWithString))
                {
                    Logger.Info($"ignoring file because hidden: {fileInfo.FullName}");
                    continue;
                }

                fileInfos.Add(fileInfo);
                Logger.Info($"added doc file file {fileInfo.FullName}");
            }

            return fileInfos;
        }


        private static string ToNormalizedDirFullName(DirectoryInfo info)
        {
            string path = info.FullName;

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString())) return path;

            return path + Path.DirectorySeparatorChar;
        }
        
        //e.g. out/ --> out
        private static string ToNormalizedDirName(string dirName)
        {
            
            if (dirName.EndsWith(Path.DirectorySeparatorChar.ToString())) 
                return dirName.Substring(0, dirName.Length - Path.DirectorySeparatorChar.ToString().Length);

            return dirName;
        }
        
    }
}
