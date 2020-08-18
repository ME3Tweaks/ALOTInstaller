using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerCore.Helpers
{
    public static class LODHelper
    {
        public static List<(string, LodSetting)> GetAvailableLODs(GameTarget target)
        {
            var options = new List<(string, LodSetting)>();
            var texturesInstalled = target.GetInstalledALOTInfo();

            LodSetting mixinSS = LodSetting.Vanilla;
            if (target.Game == Enums.MEGame.ME1)
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
    }
}
