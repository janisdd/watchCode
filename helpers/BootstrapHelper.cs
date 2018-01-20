using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using watchCode.model;

namespace watchCode.helpers
{
    public static class BootstrapHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmdArgs"></param>
        /// <param name="config">the default config overwritten with the cmd args</param>
        public static void Bootstrap(CmdArgs cmdArgs, Config config)
        {
            if (string.IsNullOrWhiteSpace(config.RootDir) == false)
            {
                DynamicConfig.AbsoluteRootDirPath = config.RootDir;
            }


            //read config if any an overwrite cmdArgs values
            if (string.IsNullOrWhiteSpace(cmdArgs.ConfigFileNameWithExtension) == false)
            {
                string absoluteConfigFilePath =
                    Path.Combine(DynamicConfig.AbsoluteRootDirPath, cmdArgs.ConfigFileNameWithExtension);

                try
                {
                    var configFileInfo = new FileInfo(absoluteConfigFilePath);

                    if (configFileInfo.Exists == false)
                    {
                        Logger.Error($"could not find config file in path: {absoluteConfigFilePath}, exitting");
                        Environment.Exit(Program.ErrorReturnCode);
                        return;
                    }

                    //try to read the config

                    var configText = File.ReadAllText(configFileInfo.FullName);

                    var readConfig = JsonConvert.DeserializeObject<Config>(configText);

                    //then take values
                    Logger.Info($"using config at: {configFileInfo.FullName}");

                    if (readConfig.InitWatchExpressionKeywords != null)
                        DynamicConfig.InitWatchExpressionKeywords = readConfig.InitWatchExpressionKeywords;

                    if (readConfig.KnownFileExtensionsWithoutExtension != null)
                        DynamicConfig.KnownFileExtensionsWithoutExtension =
                            readConfig.KnownFileExtensionsWithoutExtension;

                    var properties = readConfig
                        .GetType()
                        .GetProperties()
                        .Where(p => p.Name != nameof(readConfig.InitWatchExpressionKeywords) &&
                                    p.Name != nameof(readConfig.KnownFileExtensionsWithoutExtension)
                        );

                    //overwrite values if set
                    foreach (var property in properties)
                    {
                        object val = property.GetValue(readConfig);

                        if (val != null)
                        {
                            property.SetValue(config, val);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(
                        $"could not access/read/deserialize config file in path: {absoluteConfigFilePath}, " +
                        $"error: {e.Message} exitting");
                    Environment.Exit(Program.ErrorReturnCode);
                    return;
                }
            }


            HashHelper.SetHashAlgorithm(config.HashAlgorithmToUse);

            //check if root dir exists...

            try
            {
                var rootDirInfo = new DirectoryInfo(DynamicConfig.AbsoluteRootDirPath);
                if (rootDirInfo.Exists == false)
                {
                    Logger.Error($"root dir does not exist, path: {DynamicConfig.AbsoluteRootDirPath}, exitting");

                    Environment.Exit(Program.ErrorReturnCode);
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"error checking root dir, path: {DynamicConfig.AbsoluteRootDirPath}, error: {e.Message}, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return;
            }

            if (config.Files.Count == 0 && config.Dirs.Count == 0)
            {
                Logger.Error($"no file(s) nor dir(s) specified, returning");
                Environment.Exit(Program.OkReturnCode);
                return;
            }
        }
    }
}