using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using LegendaryExplorerCore.Packages;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    public static class LODHelper
    {
        public static List<(string, LodSetting)> GetAvailableLODs(GameTarget target)
        {
            var options = new List<(string, LodSetting)>();
            var texturesInstalled = target.GetInstalledALOTInfo();

            LodSetting mixinSS = LodSetting.Vanilla;
            if (target.Game == MEGame.ME1)
            {

                var branchingPCFCommon = Path.Combine(target.TargetPath, @"Engine", @"Shaders", @"BranchingPCFCommon.usf");
                if (File.Exists(branchingPCFCommon))
                {
                    var md5 = Utilities.CalculateMD5(branchingPCFCommon);
                    if (md5 == @"10db76cb98c21d3e90d4f0ffed55d424")
                    {
                        mixinSS = LodSetting.SoftShadows; //Add MEUITM soft shadows.
                    }
                }
            }


            if (texturesInstalled != null)
            {
                options.Add(("4K (Highest quality)", LodSetting.FourK | mixinSS));
                options.Add(("2K (Good quality)", LodSetting.TwoK | mixinSS));
            }

            options.Add(("Vanilla", LodSetting.Vanilla));
            return options;
        }

        /// <summary>
        /// Gets the LOD setting for the specified game. If an error occurs, Vanilla is returned
        /// </summary>
        /// <param name="game"></param>
        /// <param name="lods"></param>
        /// <returns></returns>
        public static LodSetting GetLODSettingFromLODs(MEGame game, Dictionary<string, string> lods)
        {
            var textureChar1024 = lods.FirstOrDefault(x => x.Key == @"TEXTUREGROUP_Character_1024");
            if (string.IsNullOrWhiteSpace(textureChar1024.Key)) //does this work for ME2/ME3??
            {
                //not found
                return LodSetting.Vanilla;
            }

            try
            {
                int maxLodSize = 0;
                int vanillaLODSize = game == MEGame.ME1 ? 1024 : 0;
                if (!string.IsNullOrWhiteSpace(textureChar1024.Value))
                {
                    //ME2,3 default to blank
                    maxLodSize = int.Parse(StringStructParser.GetCommaSplitValues(textureChar1024.Value)[game == MEGame.ME1 ? @"MinLODSize" : @"MaxLODSize"]);
                }

                if (maxLodSize != vanillaLODSize)
                {
                    //LODS MODIFIED!
                    if (maxLodSize == 4096)
                    {
                        return LodSetting.FourK;
                    }
                    else if (maxLodSize == 2048)
                    {
                        return LodSetting.TwoK;
                    }
                }

                return LodSetting.Vanilla;
            }
            catch (Exception e)
            {
                Log.Error($"[AICORE] Error getting LOD setting for {game}: {e.Message}. Returning vanilla");
                return LodSetting.Vanilla;
            }
        }
    }
}