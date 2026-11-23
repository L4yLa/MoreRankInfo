using System;
using System.IO;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Utilities;
using MoreRankInfo.Configuration;
using MoreRankInfo.Installers;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace MoreRankInfo
{
    [Plugin(RuntimeOptions.SingleStartInit), NoEnableDisable]
    public class Plugin
    {
        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger, Config config, Zenjector zenjector)
        {
            Log = logger;
            // BSIPA reads the file in Generated(), so it must be renamed before that call.
            MigrateConfigFileName();
            PluginConfig.Instance = config.Generated<PluginConfig>();
            zenjector.Install<MenuInstaller>(Location.Menu);
        }

        // Up to 0.1.1 the plugin name was "More Rank Info", so the config file was "More Rank Info.json".
        // Remove this when the minor version is next raised.
        private static void MigrateConfigFileName()
        {
            var oldPath = Path.Combine(UnityGame.UserDataPath, "More Rank Info.json");
            var newPath = Path.Combine(UnityGame.UserDataPath, "MoreRankInfo.json");
            if (!File.Exists(oldPath) || File.Exists(newPath))
                return;
            try
            {
                File.Move(oldPath, newPath);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not rename {oldPath} to {newPath}; default settings are used: {ex.Message}");
            }
        }
    }
}
