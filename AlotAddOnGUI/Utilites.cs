using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;   //This namespace is used to work with WMI classes. For using this namespace add reference of System.Management.dll .
using Microsoft.Win32;     //This namespace is used to work with Registry editor.
using System.IO;
using AlotAddOnGUI.classes;
using System.Runtime.InteropServices;
using System.Threading;
using Serilog;

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
                        "MassEffectModderNoGui");
            inipath = Path.Combine(inipath, "MassEffectModderNoGui.ini");

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
            if (path != null)
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
            }
            return null;
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

        public static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
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
            string path = null;
            switch (game)
            {
                case 1:
                    entry = "ME1VanillaBackupLocation";
                    path = Utilities.GetRegistrySettingString(entry);
                    break;
                case 2:
                    entry = "ME2VanillaBackupLocation";
                    path = Utilities.GetRegistrySettingString(entry);
                    break;
                case 3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    string softwareKey = @"HKEY_CURRENT_USER\SOFTWARE\Mass Effect 3 Mod Manager";
                    entry = "VanillaCopyLocation";
                    path = Utilities.GetRegistrySettingString(softwareKey, entry);
                    break;
                default:
                    return null;
            }
            if (path == null || !Directory.Exists(path))
            {
                return null;
            }
            if (!Directory.Exists(path + @"\BIOGame") || !Directory.Exists(path + @"\Binaries"))
            {
                return null;
            }
            return path;
        }

        // Pinvoke for API function
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

        public static bool DriveFreeBytes(string folderName, out ulong freespace)
        {
            freespace = 0;
            if (string.IsNullOrEmpty(folderName))
            {
                throw new ArgumentNullException("folderName");
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            ulong free = 0, dummy1 = 0, dummy2 = 0;

            if (GetDiskFreeSpaceEx(folderName, out free, out dummy1, out dummy2))
            {
                freespace = free;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        public static void DeleteFilesAndFoldersRecursively(string target_dir)
        {
            foreach (string file in Directory.GetFiles(target_dir))
            {
                File.Delete(file);
            }

            foreach (string subDir in Directory.GetDirectories(target_dir))
            {
                DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(1); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            Directory.Delete(target_dir);
        }

        public static bool InstallBinkw32Bypass(int game)
        {
            if (game == 1)
            {
                return false;
            }
            Log.Information("Installing binkw32 for Mass Effect " + game);
            string gamePath = GetGamePath(game);
            switch (game)
            {
                case 2:
                    gamePath += "\\Binaries\\";
                    System.IO.File.WriteAllBytes(gamePath+"binkw23.dll", AlotAddOnGUI.Properties.Resources.me2_binkw23);
                    System.IO.File.WriteAllBytes(gamePath + "binkw32.dll", AlotAddOnGUI.Properties.Resources.me2_binkw32);
                    break;
                case 3:
                    gamePath += "\\Binaries\\Win32\\";
                    System.IO.File.WriteAllBytes(gamePath + "binkw23.dll", AlotAddOnGUI.Properties.Resources.me3_binkw23);
                    System.IO.File.WriteAllBytes(gamePath + "binkw32.dll", AlotAddOnGUI.Properties.Resources.me3_binkw32);
                    break;
            }
            Log.Information("Installed binkw32 for Mass Effect " + game);
            return true;
        }
    }
}