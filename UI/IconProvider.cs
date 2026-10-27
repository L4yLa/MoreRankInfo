using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using IPA.Loader;
using MoreRankInfo.Models;
using UnityEngine;

namespace MoreRankInfo.UI
{
    /// <summary>Icon sprite and material for a leaderboard service.</summary>
    internal class ServiceIcon
    {
        public Sprite Sprite;
        public Material Material;
    }

    /// <summary>
    /// Takes the logos from the installed leaderboard mods at runtime (nothing is bundled with this mod).
    /// ScoreSaber: embedded resource ScoreSaber.Resources.logo.png.
    /// BeatLeader: its logo is drawn by BundleLoader.LogoMaterial over BundleLoader.BeatLeaderLogoGradient,
    /// so the same material is instantiated with the idle values BeatLeader's Logo component uses.
    /// Results are cached for the whole session (they are small and the mods never unload).
    /// </summary>
    internal static class IconProvider
    {
        private const string ScoreSaberLogoResource = "ScoreSaber.Resources.logo.png";
        private const string BeatLeaderBundleLoaderType = "BeatLeader.BundleLoader";

        private static readonly Dictionary<RankService, Task<ServiceIcon>> Cache = new Dictionary<RankService, Task<ServiceIcon>>();

        /// <summary>Returns null when the icon is not available.</summary>
        internal static Task<ServiceIcon> GetAsync(RankService service)
        {
            if (!Cache.TryGetValue(service, out var task) || (task.IsCompleted && task.Result == null))
            {
                task = service == RankService.ScoreSaber ? LoadScoreSaberAsync() : Task.FromResult(LoadBeatLeader());
                Cache[service] = task;
            }
            return task;
        }

        private static async Task<ServiceIcon> LoadScoreSaberAsync()
        {
            try
            {
                var assembly = PluginManager.GetPluginFromId(RankService.ScoreSaber.PluginId())?.Assembly;
                if (assembly == null)
                    return null;
                var sprite = await Utilities.LoadSpriteFromAssemblyAsync(assembly, ScoreSaberLogoResource);
                if (sprite == null)
                    return null;
                return new ServiceIcon { Sprite = sprite, Material = Utilities.ImageResources.NoGlowMat };
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"ScoreSaber icon is unavailable: {ex.Message}");
                Plugin.Log.Debug(ex);
                return null;
            }
        }

        private static ServiceIcon LoadBeatLeader()
        {
            try
            {
                var type = PluginManager.GetPluginFromId(RankService.BeatLeader.PluginId())?.Assembly.GetType(BeatLeaderBundleLoaderType);
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
                var sprite = type?.GetField("BeatLeaderLogoGradient", flags)?.GetValue(null) as Sprite;
                var logoMaterial = type?.GetField("LogoMaterial", flags)?.GetValue(null) as Material;
                if (sprite == null || logoMaterial == null)
                {
                    Plugin.Log.Warn("BeatLeader icon is unavailable (BundleLoader logo assets not found).");
                    return null;
                }

                var material = UnityEngine.Object.Instantiate(logoMaterial);
                material.name = "MoreRankInfo BeatLeader Logo";
                // Idle values of BeatLeader's Logo component (Logo.cs).
                SetFloat(material, "_Glow", 0.15f);
                SetFloat(material, "_DotScale", 0.25f);
                SetFloat(material, "_BlockScale", 0.7f);
                SetFloat(material, "_CornerRadius", 0.2f);
                SetFloat(material, "_Thickness", 0.08f);
                SetFloat(material, "_SpinnerAmplitude", 1.0f);
                SetFloat(material, "_SpinnerRotation", 0f);
                return new ServiceIcon { Sprite = sprite, Material = material };
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"BeatLeader icon is unavailable: {ex.Message}");
                Plugin.Log.Debug(ex);
                return null;
            }
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
                material.SetFloat(name, value);
        }
    }
}
