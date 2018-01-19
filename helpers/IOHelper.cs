using System;
using System.IO;

namespace watchCode.helpers
{
    public static class IoHelper
    {
        public static bool EnsureDirExists(string absoluteSnapshotDirectoryPath)
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
                    $"could not opne/create dir: {absoluteSnapshotDirectoryPath}, error: {e.Message}");
                return false;
            }

            return true;
        }


        public static bool CheckFileExists(string absoluteFilePath, bool reportErrorIfNotExists)
        {
            try
            {
                var fileInfo = new FileInfo(absoluteFilePath);

                if (fileInfo.Exists == false)
                {
                    if (reportErrorIfNotExists)
                        Logger.Error($"file does not exists, path: {absoluteFilePath}");

                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"could access file at: {absoluteFilePath}, error: {e.Message}");
                return false;
            }

            return true;
        }

        //from https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory
        //modified
        public static string GetRelativePath(string absoluteFilePath, string absoluteFolderPath)
        {
            //see https://github.com/dotnet/corefx/issues/1745
            //use C: at front to be ablue to use this with dotnet core 1.x

            //TODO not sure if this always works correctly...

            absoluteFilePath = "C:" + absoluteFilePath;
            absoluteFolderPath = "C:" + absoluteFolderPath;
            absoluteFilePath = absoluteFilePath.Replace(Path.DirectorySeparatorChar, '\\');
            absoluteFolderPath = absoluteFolderPath.Replace(Path.DirectorySeparatorChar, '\\');

            var pathUri = new Uri(absoluteFilePath);
            // Folders must end in a slash
            if (!absoluteFolderPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                absoluteFolderPath += Path.DirectorySeparatorChar;
            }
            var folderUri = new Uri(absoluteFolderPath);

            var relativePath = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));
            
            return relativePath;
        }
    }
}