using IPA;
using IPA.Config;
using IPA.Config.Stores;
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
            PluginConfig.Instance = config.Generated<PluginConfig>();
            zenjector.Install<MenuInstaller>(Location.Menu);
        }
    }
}
