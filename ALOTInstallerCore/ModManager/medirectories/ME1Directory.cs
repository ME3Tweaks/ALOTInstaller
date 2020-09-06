﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace ALOTInstallerCore.ModManager.GameDirectories
{
    [Localizable(false)]
    public static class ME1Directory
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
                    if (value.Contains("BioGame"))
                        value = value.Substring(0, value.LastIndexOf("BioGame"));
                }
                _gamePath = value;
            }
        }

        internal static string CookedPath(string basepath) => Path.Combine(basepath, @"BioGame\CookedPC");
        public static string bioGamePath => gamePath != null ? Path.Combine(gamePath, @"BioGame\") : null;
        public static string cookedPath => gamePath != null ? Path.Combine(gamePath, @"BioGame\CookedPC\") : "Not Found";
        public static string CookedPath(GameTarget target) => Path.Combine(target.TargetPath, @"BioGame\CookedPC");
        public static string DLCPath => gamePath != null ? Path.Combine(gamePath, @"DLC\") : "Not Found";


        // "C:\...\MyDocuments\BioWare\Mass Effect\" folder
        public static string BioWareDocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"\BioWare\Mass Effect\");
        public static string GamerSettingsIniFile => Path.Combine(BioWareDocPath, @"BIOGame\Config\GamerSettings.ini");
        public static List<string> VanillaDlls = new List<string>
        {
            "binkw23.dll",
            "binkw32.dll",
            "MassEffectGDF.dll",
            "NxCooking.dll",
            "ogg.dll",
            "OpenAL32.dll",
            "PhysXCore.dll",
            "PhysXLoader.dll",
            "unicows.dll",
            "unrar.dll",
            "vorbis.dll",
            "vorbisfile.dll",
            "WINUI.dll",
            "wrap_oal.dll"
        };

        internal static string ASIPath(GameTarget target) => Path.Combine(target.TargetPath, "Binaries", "asi");

        static ME1Directory()
        {
            ReloadActivePath();
        }

        public static void ReloadActivePath()
        {
#if WINDOWS
            string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
            string subkey = @"BioWare\Mass Effect";

            string keyName = hkey32 + subkey;
            string test = (string)Registry.GetValue(keyName, "Path", null);
            if (test != null)
            {
                gamePath = test;
                return;
            }

            keyName = hkey64 + subkey;
            gamePath = (string)Registry.GetValue(keyName, "Path", null);
#endif
        }

        public static string ExecutablePath(string gameRoot) => Path.Combine(gameRoot, "Binaries", "MassEffect.exe");


        public static Dictionary<string, string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
            ["DLC_UNC"] = "Bring Down the Sky",
            ["DLC_Vegas"] = "Pinnacle Station"
        };

        public static List<string> OfficialDLC = new List<string>
        {
            "DLC_UNC",
            "DLC_Vegas"
        };
    }
}
