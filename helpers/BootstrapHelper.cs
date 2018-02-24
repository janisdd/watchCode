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
            if (string.IsNullOrWhiteSpace(config.DocFilesDirAbsolutePath) == false)
            {
                DynamicConfig.DocFilesDirAbsolutePath = config.DocFilesDirAbsolutePath;
            }
            
            if (string.IsNullOrWhiteSpace(config.SourceFilesDirAbsolutePath) == false)
            {
                DynamicConfig.SourceFilesDirAbsolutePath = config.SourceFilesDirAbsolutePath;
            }


            //read config if any an overwrite cmdArgs values
            if (string.IsNullOrWhiteSpace(cmdArgs.ConfigFileNameWithExtension) == false)
            {
                string absoluteConfigFilePath =
                    Path.Combine(DynamicConfig.DocFilesDirAbsolutePath, cmdArgs.ConfigFileNameWithExtension);

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
                    {
                        DynamicConfig.InitWatchExpressionKeywords = readConfig.InitWatchExpressionKeywords;
                    }

                    if (readConfig.KnownFileExtensionsWithoutExtension != null)
                    {
                        DynamicConfig.KnownFileExtensionsWithoutExtension =
                            readConfig.KnownFileExtensionsWithoutExtension;
                    }

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
                var rootDirInfo = new DirectoryInfo(DynamicConfig.DocFilesDirAbsolutePath);
                if (rootDirInfo.Exists == false)
                {
                    Logger.Error($"root doc dir does not exist, path: {DynamicConfig.DocFilesDirAbsolutePath}, exitting");

                    Environment.Exit(Program.ErrorReturnCode);
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"error checking doc root dir, path: {DynamicConfig.DocFilesDirAbsolutePath}, error: {e.Message}, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return;
            }
            
            try
            {
                var rootDirInfo = new DirectoryInfo(DynamicConfig.SourceFilesDirAbsolutePath);
                if (rootDirInfo.Exists == false)
                {
                    Logger.Error($"root source dir does not exist, path: {DynamicConfig.SourceFilesDirAbsolutePath}, exitting");

                    Environment.Exit(Program.ErrorReturnCode);
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"error checking source root dir, path: {DynamicConfig.SourceFilesDirAbsolutePath}, error: {e.Message}, exitting");
                Environment.Exit(Program.ErrorReturnCode);
                return;
            }
            

            
        }
    }
}
