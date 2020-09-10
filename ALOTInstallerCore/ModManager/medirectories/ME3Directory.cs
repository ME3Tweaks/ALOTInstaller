﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace ALOTInstallerCore.ModManager.GameDirectories

{
    [Localizable(false)]
    public static class ME3Directory
    {
        private static string _gamePath;
        public static string gamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                    return null;
                return Path.GetFullPath(_gamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BIOGame"))
                        value = value.Substring(0, value.LastIndexOf("BIOGame"));
                }
                _gamePath = value;
            }
        }

        internal static string CookedPath(string basepath) => Path.Combine(basepath, @"BioGame\CookedPCConsole");
        public static string biogamePath => gamePath != null ? gamePath.Contains("biogame", StringComparison.OrdinalIgnoreCase) ? gamePath : Path.Combine(gamePath, @"BIOGame\") : null;
        public static string tocFile => gamePath != null ? Path.Combine(gamePath, @"BIOGame\PCConsoleTOC.bin") : null;
        public static string cookedPath => gamePath != null ? Path.Combine(gamePath, @"BIOGame\CookedPCConsole\") : "Not Found";
        public static string CookedPath(GameTarget target) => Path.Combine(target.TargetPath, @"BioGame\CookedPCConsole");

        /// <summary>
        /// Gets the path to the testpatch DLC file for the specified game target
        /// </summary>
        /// <param name="target">target to resolve path for</param>
        /// <returns>Null if gametarget game is not ME3. Path where SFAR should be if ME3.</returns>
        public static string GetTestPatchPath(GameTarget target)
        {
            if (target.Game != Enums.MEGame.ME3) return null;
            return Path.Combine(target.TargetPath, @"BIOGame\Patches\PCConsole\Patch_001.sfar");
        }
        public static string DLCPath => gamePath != null ? Path.Combine(gamePath, @"BIOGame\DLC\") : "Not Found";

        // "C:\...\MyDocuments\BioWare\Mass Effect 3\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect 3\");
        public static string GamerSettingsIniFile => Path.Combine(BioWareDocPath, @"BIOGame\Config\GamerSettings.ini");

        internal static string ASIPath(GameTarget target) => Path.Combine(target.TargetPath, "Binaries", "Win32", "asi");


        public static string ExecutablePath(string gameRoot) => Path.Combine(gameRoot, "Binaries", "Win32", "MassEffect3.exe");
        public static List<string> VanillaDlls = new List<string>
        {
            "atiags.dll",
            "binkw32.dll",
            "binkw23.dll", //We put this here so this is not detected as non-vanilla since almost all modded games will have this
            "PhysXExtensions.dll"
        };
        static ME3Directory()
        {
            ReloadActivePath();
        }

        public static void ReloadActivePath()
        {
            #if WINDDOWS
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect 3";

            // Check 64-bit first
            string keyName = hkey64 + subkey;
            string test = (string)Registry.GetValue(keyName, "Install Dir", null);
            if (test != null)
            {
                gamePath = test;
                return;
            }

            keyName = hkey32 + subkey;
            gamePath = (string)Microsoft.Win32.Registry.GetValue(keyName, "Install Dir", null);
        #endif
        }

        public static Dictionary<string, string> OfficialDLCNames = new CaseInsensitiveDictionary<string>()
        {
            ["DLC_HEN_PR"] = "From Ashes",
            ["DLC_OnlinePassHidCE"] = "Collectors Edition Content",
            ["DLC_CON_MP1"] = "Resurgence",
            ["DLC_CON_MP2"] = "Rebellion",
            ["DLC_CON_MP3"] = "Earth",
            ["DLC_CON_END"] = "Extended Cut",
            ["DLC_CON_GUN01"] = "Firefight Pack",
            ["DLC_EXP_Pack001"] = "Leviathan",
            ["DLC_UPD_Patch01"] = "Multiplayer Balance Changes Cache 1",
            ["DLC_CON_MP4"] = "Retaliation",
            ["DLC_CON_GUN02"] = "Groundside Resistance Pack",
            ["DLC_EXP_Pack002"] = "Omega",
            ["DLC_CON_APP01"] = "Alternate Appearance Pack 1",
            ["DLC_UPD_Patch02"] = "Multiplayer Balance Changes Cache 2",
            ["DLC_CON_MP5"] = "Reckoning",
            ["DLC_EXP_Pack003_Base"] = "Citadel - Part I",
            ["DLC_EXP_Pack003"] = "Citadel - Part II",
            ["DLC_CON_DH1"] = "Genesis 2",
            ["DLC_TestPatch"] = "TESTPATCH (Patch_001.sfar)"

        };

        public static List<string> OfficialDLC = new List<string>
        {
            "DLC_OnlinePassHidCE",
            "DLC_CON_MP1",
            "DLC_CON_MP2",
            "DLC_CON_MP3",
            "DLC_CON_MP4",
            "DLC_CON_MP5",
            "DLC_UPD_Patch01",
            "DLC_UPD_Patch02",
            "DLC_HEN_PR",
            "DLC_CON_END",
            "DLC_EXP_Pack001",
            "DLC_EXP_Pack002",
            "DLC_EXP_Pack003_Base",
            "DLC_EXP_Pack003",
            "DLC_CON_GUN01",
            "DLC_CON_GUN02",
            "DLC_CON_APP01",
            "DLC_CON_DH1"
        };
    }
}
