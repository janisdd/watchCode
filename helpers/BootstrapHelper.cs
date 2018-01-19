using System;
using System.IO;
using watchCode.model;

namespace watchCode.helpers
{
    public static class BootstrapHelper
    {
        public static void Bootstrap(CmdArgs cmdArgs)
        {
            if (string.IsNullOrWhiteSpace(cmdArgs.RootDir) == false)
            {
                DynamicConfig.AbsoluteRootDirPath = cmdArgs.RootDir;
            }
            
            HashHelper.SetHashAlgorithm(cmdArgs.HashAlgorithmToUse);
            
            //check if root dir exists...

            try
            {
                var rootDirInfo = new DirectoryInfo(DynamicConfig.AbsoluteRootDirPath);
                if (rootDirInfo.Exists == false)
                {
                    Logger.Error($"root dir does not exist, path: {DynamicConfig.AbsoluteRootDirPath}");

                    Environment.Exit(Program.ErrorReturnCode);
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"error checking root dir, path: {DynamicConfig.AbsoluteRootDirPath}, error: {e.Message}");
                Environment.Exit(Program.ErrorReturnCode);
                return;
            }

            if (cmdArgs.Files.Count == 0 && cmdArgs.Dirs.Count == 0)
            {
                Logger.Error($"no file(s) nor dir(s) specified, returning");
                Environment.Exit(Program.OkReturnCode);
                return;
            }
        }
    }
}