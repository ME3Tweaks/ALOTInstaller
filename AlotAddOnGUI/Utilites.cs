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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml;
using System.Windows;
using System.Xml.Linq;

namespace AlotAddOnGUI
{
    public class Utilities
    {
        public const uint MEMI_TAG = 0x494D454D;
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
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
        public static String GetGamePath(int gameID, bool allowMissingEXE = false)
        {
            //Read config file.
            string path = null;
            string mempath = null;
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
                    mempath = path;
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

                    if (!File.Exists(GameEXEPath))
                        path = null; //mem path is not valid. might still be able to return later.
                    else
                        return path;
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
            if (mempath != null && allowMissingEXE)
            {
                return mempath;
            }
            return null;
        }

        public static string GetGameEXEPath(int game)
        {
            string path = GetGamePath(game);
            if (path == null) { return null; }
            switch (game)
            {
                case 1:
                    return Path.Combine(path, @"Binaries\MassEffect.exe");
                case 2:
                    return Path.Combine(path, @"Binaries\MassEffect2.exe");
                case 3:
                    return Path.Combine(path, @"Binaries\Win32\MassEffect3.exe");
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

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, bool data)
        {
            WriteRegistryKey(subkey, subpath, value, data ? 1 : 0);
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

        public static bool DeleteFilesAndFoldersRecursively(string target_dir)
        {
            bool result = true;
            foreach (string file in Directory.GetFiles(target_dir))
            {
                File.SetAttributes(file, FileAttributes.Normal); //remove read only
                try
                {
                    //Debug.WriteLine("Deleting file: " + file);
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Log.Error("Unable to delete file: " + file + ". It may be open still");
                    return false;
                }
            }

            foreach (string subDir in Directory.GetDirectories(target_dir))
            {
                result &= DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(4); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            try
            {
                //Debug.WriteLine("Deleting directory: " + target_dir);

                Directory.Delete(target_dir);
            }
            catch (Exception e)
            {
                Log.Error("Unable to delete directory: " + target_dir + ". It may be open still");
                return false;
            }
            return result;
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
                    System.IO.File.WriteAllBytes(gamePath + "binkw23.dll", AlotAddOnGUI.Properties.Resources.me2_binkw23);
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

        /// <summary>
        /// Creates a marker file using the specified information as well as the current MEM (no GUI) and Installer release version
        /// </summary>
        /// <param name="game"></param>
        /// <param name="alotVersionInfo"></param>
        public static void CreateMarkerFile(int game, ALOTVersionInfo alotVersionInfo)
        {
            using (FileStream fs = new FileStream(GetALOTMarkerFilePath(game), FileMode.Open, FileAccess.Write))
            {
                fs.SeekEnd();
                fs.WriteInt32(alotVersionInfo.MEUITMVER); //MEUITM version. Not used for now //-16
                fs.WriteInt16(alotVersionInfo.ALOTVER); //-12
                fs.WriteByte(alotVersionInfo.ALOTUPDATEVER); //-10
                fs.WriteByte(alotVersionInfo.ALOTHOTFIXVER); //-9

                //Get versions
                var versInfo = FileVersionInfo.GetVersionInfo(MainWindow.BINARY_DIRECTORY + "MassEffectModderNoGui.exe");
                short memVersionUsed = (short)versInfo.FileMajorPart;

                //Get current installer release version
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
                short installerVersionUsed = Convert.ToInt16(fvi.FileBuildPart);

                fs.WriteInt16(memVersionUsed); // -8
                fs.WriteInt16(installerVersionUsed); // -6
                fs.WriteUInt32(MEMI_TAG); // -4
            }
        }

        internal static string GetALOTMarkerFilePath(int gameID)
        {
            string gamePath = Utilities.GetGamePath(gameID);
            if (gamePath != null)
            {

                if (gameID == 1)
                {
                    gamePath += @"\BioGame\CookedPC\testVolumeLight_VFX.upk";
                }
                if (gameID == 2)
                {
                    gamePath += @"\BioGame\CookedPC\BIOC_Materials.pcc";
                }
                if (gameID == 3)
                {
                    gamePath += @"\BIOGame\CookedPCConsole\adv_combat_tutorial_xbox_D_Int.afc";
                }
                return gamePath;
            }
            return null;
        }

        public static ALOTVersionInfo GetInstalledALOTInfo(int gameID)
        {
            string gamePath = Utilities.GetALOTMarkerFilePath(gameID);
            if (gamePath != null && File.Exists(gamePath))
            {
                using (FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read))
                {
                    fs.SeekEnd();
                    long endPos = fs.Position;
                    fs.Position = endPos - 4;
                    uint memi = fs.ReadUInt32();

                    if (memi == MEMI_TAG)
                    {
                        //ALOT has been installed
                        fs.Position = endPos - 8;
                        int installerVersionUsed = fs.ReadInt32();
                        int perGameFinal4Bytes = -20;
                        switch (gameID)
                        {
                            case 1:
                                perGameFinal4Bytes = 0;
                                break;
                            case 2:
                                perGameFinal4Bytes = 4352;
                                break;
                            case 3:
                                perGameFinal4Bytes = 16777472;
                                break;
                        }

                        if (installerVersionUsed >= 10 && installerVersionUsed != perGameFinal4Bytes) //default bytes before 178 MEMI Format
                        {
                            fs.Position = endPos - 12;
                            short ALOTVER = fs.ReadInt16();
                            byte ALOTUPDATEVER = (byte)fs.ReadByte();
                            byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                            //unused for now
                            fs.Position = endPos - 16;
                            int MEUITMVER = fs.ReadInt32();

                            return new ALOTVersionInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER);
                        }
                        else
                        {
                            return new ALOTVersionInfo(0, 0, 0, 0); //MEMI tag but no info we know of
                        }
                    }
                }
            }
            return null;
        }

        public static int runProcess(string exe, string args, bool standAlone = false)
        {
            Log.Information("Running process: " + exe + " " + args);
            using (Process p = new Process())
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.FileName = exe;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.Arguments = args;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                {
                    p.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            output.AppendLine(e.Data);
                        }
                    };
                    p.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            error.AppendLine(e.Data);
                        }
                    };

                    p.Start();
                    if (!standAlone)
                    {
                        int timeout = 600000;
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();

                        if (p.WaitForExit(timeout) &&
                            outputWaitHandle.WaitOne(timeout) &&
                            errorWaitHandle.WaitOne(timeout))
                        {
                            // Process completed. Check process.ExitCode here.
                            string outputmsg = "Process output of " + exe + " " + args + ":";
                            if (output.ToString().Length > 0)
                            {
                                outputmsg += "\nStandard:\n" + output.ToString();
                            }
                            if (error.ToString().Length > 0)
                            {
                                outputmsg += "\nError:\n" + error.ToString();
                            }
                            Log.Information(outputmsg);
                            return p.ExitCode;
                        }
                        else
                        {
                            // Timed out.
                            Log.Error("Process timed out: " + exe + " " + args);
                            return -1;
                        }
                    }
                    else
                    {
                        return 0; //standalone
                    }
                }
            }
        }
        public static Task DeleteAsync(string path)
        {
            if (!File.Exists(path))
            {
                return Task.FromResult(0);
            }
            return Task.Run(() => { File.Delete(path); });
        }

        public static Task<FileStream> CreateAsync(string path)
        {
            if (path == null || path == "")
            {
                return null;
            }
            return Task.Run(() => File.Create(path));
        }

        public static Task MoveAsync(string sourceFileName, string destFileName)
        {
            if (sourceFileName == null || sourceFileName == "")
            {
                return null;
            }
            if (!File.Exists(sourceFileName))
            {
                return null;
            }
            return Task.Run(() => { File.Move(sourceFileName, destFileName); });
        }

        public static void GrantAccess(string fullPath)
        {
            DirectoryInfo dInfo = new DirectoryInfo(fullPath);
            DirectorySecurity dSecurity = dInfo.GetAccessControl();
            dSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
            dInfo.SetAccessControl(dSecurity);
        }

        public static void TurnOffOriginAutoUpdateForGame(int game)
        {
            Log.Information("Attempting to disable auto update support for game: " + game);
            string gamePath = GetGamePath(game);
            if (gamePath != null && Directory.Exists(gamePath))
            {
                gamePath += @"\__Installer\installerdata.xml";
                if (File.Exists(gamePath))
                {
                    //Origin installer file
                    string newValue = string.Empty;
                    XmlDocument xmlDoc = new XmlDocument();

                    xmlDoc.Load(gamePath);

                    XmlNode node = xmlDoc.SelectSingleNode("game/metadata/featureFlags");
                    if (node != null)
                    {
                        //set settings same as me3
                        XmlAttribute attr = xmlDoc.CreateAttribute("autoUpdateEnabled");
                        attr.Value = 0.ToString();
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("useGameVersionFromManifestEnabled");
                        attr.Value = 1.ToString();
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("treatUpdatesAsMandatory");
                        attr.Value = 0.ToString();
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("forceTouchupInstallerAfterUpdate");
                        attr.Value = 0.ToString();
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("useGameVersionFromManifestEnabled");
                        attr.Value = 1.ToString();
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("enableDifferentialUpdate");
                        attr.Value = 1.ToString();
                        SetAttrSafe(node, attr);

                        xmlDoc.Save(gamePath);
                    }
                }
                else
                {
                    Log.Information("Installer manifest does not exist. This does not appear to be an origin installation. Skipping this step");
                }
            }
        }

        private static void SetAttrSafe(XmlNode node, params XmlAttribute[] attrList)
        {
            foreach (var attr in attrList)
            {
                if (node.Attributes[attr.Name] != null)
                {
                    node.Attributes[attr.Name].Value = attr.Value;
                }
                else
                {
                    node.Attributes.Append(attr);
                }
            }
        }

        public static long GetInstalledRamAmount()
        {
            long memKb;
            GetPhysicallyInstalledSystemMemory(out memKb);
            return memKb;
        }

        public static bool isRunningOnAMD()
        {
            var processorIdentifier = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            return processorIdentifier != null && processorIdentifier.Contains("AuthenticAMD");
        }

        public static bool TestXMLIsValid(string inputXML)
        {
            try
            {
                XDocument.Parse(inputXML);
                return true;
            }
            catch (XmlException e)
            {
                return false;
            }
        }

        public static string sha256(string randomString)
        {
            System.Security.Cryptography.SHA256Managed crypt = new System.Security.Cryptography.SHA256Managed();
            System.Text.StringBuilder hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString), 0, Encoding.UTF8.GetByteCount(randomString));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static void OpenAndSelectFileInExplorer(string filePath)
        {
            // suppose that we have a test.txt at E:\
            if (!File.Exists(filePath))
            {
                return;
            }

            // combine the arguments together
            // it doesn't matter if there is a space after ','
            string argument = "/select, \"" + filePath + "\"";

            System.Diagnostics.Process.Start("explorer.exe", argument);
        }

        public static bool IsWindowOpen<T>(string name = "") where T : Window
        {
            return string.IsNullOrEmpty(name)
               ? Application.Current.Windows.OfType<T>().Any()
               : Application.Current.Windows.OfType<T>().Any(w => w.Name.Equals(name));
        }

        public static long DirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size;
        }

        public static void GetAntivirusInfo()
        {
            ManagementObjectSearcher wmiData = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntivirusProduct");
            ManagementObjectCollection data = wmiData.Get();

            foreach (ManagementObject virusChecker in data)
            {
                var virusCheckerName = virusChecker["displayName"];
                var productState = virusChecker["productState"];
                uint productVal = (uint)productState;
                var bytes = BitConverter.GetBytes(productVal);
                Log.Information("Antivirus info: " + virusCheckerName + " with state " + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + " " + bytes[3].ToString("X2"));
            }
        }

        public static bool isAntivirusRunning()
        {
            return true;
        }


        public static bool isGameRunning(int gameID)
        {
            if (gameID == 1)
            {
                Process[] pname = Process.GetProcessesByName("MassEffect");
                return pname.Length > 0;
            }
            if (gameID == 2)
            {
                Process[] pname = Process.GetProcessesByName("MassEffect2");
                Process[] pname2 = Process.GetProcessesByName("ME2Game");
                return pname.Length > 0 || pname2.Length > 0;
            }
            else
            {
                Process[] pname = Process.GetProcessesByName("MassEffect3");
                return pname.Length > 0;
            }

        }
    }
}