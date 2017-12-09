using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;   //This namespace is used to work with WMI classes. For using this namespace add reference of System.Management.dll .
using Microsoft.Win32;     //This namespace is used to work with Registry editor.
using System.IO;
using AlotAddOnGUI.classes;

namespace AlotAddOnGUI
{
    public class Utilities
    {
        public static string GetOperatingSystemInfo()
        {
            StringBuilder sb = new StringBuilder();
            //Create an object of ManagementObjectSearcher class and pass query as parameter.
            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
            foreach (ManagementObject managementObject in mos.Get())
            {
                if (managementObject["Caption"] != null)
                {
                    sb.AppendLine("Operating System Name  :  " + managementObject["Caption"].ToString());   //Display operating system caption
                }
                if (managementObject["OSArchitecture"] != null)
                {
                    sb.AppendLine("Operating System Architecture  :  " + managementObject["OSArchitecture"].ToString());   //Display operating system architecture.
                }
                if (managementObject["CSDVersion"] != null)
                {
                    sb.AppendLine("Operating System Service Pack   :  " + managementObject["CSDVersion"].ToString());     //Display operating system version.
                }
            }
            sb.AppendLine("\nProcessor Information-------");
            RegistryKey processor_name = Registry.LocalMachine.OpenSubKey(@"Hardware\Description\System\CentralProcessor\0", RegistryKeyPermissionCheck.ReadSubTree);   //This registry entry contains entry for processor info.

            if (processor_name != null)
            {
                if (processor_name.GetValue("ProcessorNameString") != null)
                {
                    sb.AppendLine((string)processor_name.GetValue("ProcessorNameString"));   //Display processor ingo.
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the MEM game path. If the MEM game path is not set, the one from the registry is used.
        /// </summary>
        /// <param name="gameID"></param>
        /// <returns></returns>
        public static String GetGamePath(int gameID)
        {
            //Read config file.
            string path = null;
            string inipath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MassEffectModder");
            inipath = Path.Combine(inipath, "MassEffectModder.ini");

            if (File.Exists(inipath))
            {
                IniFile configIni = new IniFile(inipath);
                string key = "ME" + gameID;
                path = configIni.Read(key, "GameDataPath");
                if (path != null && path != "")
                {
                    path = path.TrimEnd(Path.DirectorySeparatorChar);

                    string GameEXEPath = "";
                    switch (gameID)
                    {
                        case 1:
                            GameEXEPath = Path.Combine(path, @"Binaries\MassEffect.exe");
                            break;
                        case 2:
                            GameEXEPath = Path.Combine(path, @"Binaries\MassEffect2.exe");
                            break;
                        case 3:
                            GameEXEPath = Path.Combine(path, @"Binaries\Win32\MassEffect3.exe");
                            break;
                    }

                    if (File.Exists(GameEXEPath))
                        return path; //we have path now
                    else
                        path = null; //use registry key
                }
            }

            //does not exist in ini (or ini does not exist).
            string softwareKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
            string key64 = @"Wow6432Node\";
            string gameKey = @"BioWare\Mass Effect";
            string entry = "Path";

            if (gameID == 2)
                gameKey += @" 2";
            else if (gameID == 3)
            {
                gameKey += @" 3";
                entry = "Install Dir";
            }

            path = (string)Registry.GetValue(softwareKey + gameKey, entry, null);
            if (path == null)
            {
                path = (string)Registry.GetValue(softwareKey + key64 + gameKey, entry, null);
            }
            return path;
        }

        public static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, string data)
        {
            int i = 0;
            string[] subkeys = subpath.Split('\\');
            while (i < subkeys.Length)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }
            subkey.SetValue(value, data);
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, int data)
        {
            int i = 0;
            string[] subkeys = subpath.Split('\\');
            while (i < subkeys.Length)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }
            subkey.SetValue(value, data);
        }

        public static string GetRegistrySettingString(string name)
        {
            string softwareKey = @"HKEY_CURRENT_USER\" + MainWindow.REGISTRY_KEY;
            return (string)Registry.GetValue(softwareKey, name, null);
        }

        public static bool? GetRegistrySettingBool(string name)
        {
            string softwareKey = @"HKEY_CURRENT_USER\" + MainWindow.REGISTRY_KEY;

            int? value = (int?)Registry.GetValue(softwareKey, name, null);
            if (value != null)
            {
                return value > 0;
            }
            return null;
        }

        public static string GetGameBackupPath(int game)
        {
            string entry = "";
            switch (game)
            {
                case 1:
                    entry = "ME1VanillaBackupLocation";
                    return Utilities.GetRegistrySettingString(entry);
                case 2:
                    entry = "ME2VanillaBackupLocation";
                    return Utilities.GetRegistrySettingString(entry);
                case 3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    string softwareKey = @"HKEY_CURRENT_USER\SOFTWARE\Mass Effect 3 Mod Manager";
                    entry = "VanillaCopyLocation";
                    return (string)Registry.GetValue(softwareKey, entry, null);
                default:
                    return null;
            }
        }
    }
}