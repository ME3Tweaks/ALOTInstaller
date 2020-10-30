using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
//This namespace is used to work with Registry editor.
using System.IO;
using System.Threading;
using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using System.Reflection;
using ALOTInstallerCore.Objects;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using ALOTInstallerCore.Helpers;
using Microsoft.Win32;
using NickStrupat;
using System.Management;
using ALOTInstallerCore.Helpers.AppSettings;
using ME3ExplorerCore.Gammtek.Extensions.Reflection;

namespace ALOTInstallerCore
{
    public static partial class Utilities
    {
        /// <summary>
        /// Used to denote the texture marker on the main information marker file
        /// </summary>
        public const uint MEMI_TAG = 0x494D454D;

        /// <summary>
        /// Logs information about the system
        /// </summary>
        public static void LogOperatingSystemInfo()
        {
            StringBuilder sb = new StringBuilder();
            var ci = new ComputerInfo();

            Log.Information($"[AICORE] Operating system: {ci.OSFullName}");
            //sb.AppendLine("Version " + osBuildVersion);
            //sb.AppendLine(GetCPUString());
            Log.Information("[AICORE] System Memory: " + FileSizeFormatter.FormatSize(ci.TotalPhysicalMemory));
            Log.Information($"[AICORE] System Culture: {ci.InstalledUICulture.Name}");
            //+ Thread.CurrentThread.CurrentCulture.Name);
            //return sb.ToString();

            //return "";
        }

        /// <summary>
        /// Gets the folder of the current program that is running this library.
        /// </summary>
        /// <returns></returns>
        internal static string GetExecutingAssemblyFolder() => Path.GetDirectoryName(GetExecutablePath());

        /// <summary>
        /// Gets a resource from ALOTInstallerCore
        /// </summary>
        /// <param name="assemblyResource"></param>
        /// <returns></returns>
        private static Stream GetResourceStream(string assemblyResource)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var res = assembly.GetManifestResourceNames();
            return assembly.GetManifestResourceStream(assemblyResource);
        }


        internal static MemoryStream ExtractInternalFileToStream(string internalResourceName)
        {
            Log.Information("[AICORE] Extracting embedded file: " + internalResourceName + " to memory");
#if DEBUG
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
#endif
            using Stream stream = Utilities.GetResourceStream(internalResourceName);
            MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Extracts an embedded file from this assembly. 
        /// </summary>
        /// <param name="internalResourceName"></param>
        /// <param name="destination"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        internal static string ExtractInternalFile(string internalResourceName, string destination, bool overwrite)
        {
            Log.Information("[AICORE] Extracting embedded file: " + internalResourceName + " to " + destination);
#if DEBUG
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
#endif
            if (!File.Exists(destination) || overwrite || new FileInfo(destination).Length == 0)
            {

                using (Stream stream = Utilities.GetResourceStream(internalResourceName))
                {
                    if (File.Exists(destination))
                    {
                        FileInfo fi = new FileInfo(destination);
                        if (fi.IsReadOnly)
                        {
                            fi.IsReadOnly = false; //clear read only. might happen on some binkw32 in archives, maybe
                        }
                    }

                    using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(file);
                    }
                }
            }
            else
            {
                Log.Warning("[AICORE] File already exists. Not overwriting file.");
            }

            return destination;
        }

        /// <summary>
        /// Opens a web page. This works cross platform
        /// </summary>
        /// <param name="url"></param>
        public static void OpenWebPage(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public static bool IsWindows10OrNewer()
        {
#if WINDOWS
            var os = Environment.OSVersion;
            return os.Platform == PlatformID.Win32NT &&
                   (os.Version.Major >= 10);
#endif
            return true;
        }

        /// <summary> Checks for write access for the given folder, using a subfile named temp_alot.txt.
        /// </summary>
        /// <param name="fileName">The filename.</param>
        /// <returns>true, if write access is allowed, otherwise false</returns>
        public static bool IsDirectoryWritable(string dir)
        {
            var files = Directory.GetFiles(dir);
            try
            {
                System.IO.File.Create(Path.Combine(dir, "temp_alot.txt")).Close();
                System.IO.File.Delete(Path.Combine(dir, "temp_alot.txt"));
                return true;
            }
            catch (System.UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Error checking permissions to folder: " + dir);
                Log.Error("[AICORE] Directory write test had error that was not UnauthorizedAccess: " + e.Message);
            }
            return false;
        }

        // Todo: Move to MEDirectories?
        /// <summary>
        /// Given a game and executable path, returns the basepath of the installation.
        /// </summary>
        /// <param name="game">What game this exe is for</param>
        /// <param name="exe">Executable path</param>
        /// <returns></returns>
        public static string GetGamePathFromExe(Enums.MEGame game, string exe)
        {
            try
            {
                string result = Path.GetDirectoryName(Path.GetDirectoryName(exe)); //binaries, <GAME>

                if (game == Enums.MEGame.ME3)
                    result = Path.GetDirectoryName(result); //up one more because of win32 directory.
                return result;
            }
            catch (Exception e)
            {
                // Can't possibly be game
                return null;
            }
        }


        /// <summary>
        /// Reads all lines from a file, attempting to do so even if the file is in use by another process
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string[] WriteSafeReadAllLines(String path)
        {
            using var csv = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(csv);
            List<string> file = new List<string>();
            while (!sr.EndOfStream)
            {
                file.Add(sr.ReadLine());
            }

            return file.ToArray();
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

        public static List<KeyValuePair<string, string>> SUPPORTED_HASHES_ME1 = new List<KeyValuePair<string, string>>();
        public static List<KeyValuePair<string, string>> SUPPORTED_HASHES_ME2 = new List<KeyValuePair<string, string>>();
        public static List<KeyValuePair<string, string>> SUPPORTED_HASHES_ME3 = new List<KeyValuePair<string, string>>();

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
                    Log.Error("[AICORE] Unable to delete file: " + file + ". It may be open still: " + e.Message);
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
                Log.Information($"[AICORE] Deleting directory {target_dir}");
                Directory.Delete(target_dir);
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Unable to delete directory: " + target_dir + ". It may be open still. " + e.Message);
                return false;
            }
            return result;
        }

        public static string CalculateMD5(string filename)
        {
            if (!File.Exists(filename))
            {
                Log.Error($"[AICORE] Cannot hash file that doesn't exist: {filename}");
                return null;
            }

            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filename);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (IOException e)
            {
                Log.Error("[AICORE] I/O ERROR CALCULATING CHECKSUM OF FILE: " + filename);
                e.WriteToLog("[AICORE] ");
                return null;
            }
        }

        /// <summary>
        /// Calculates the MD5 of the stream from the beginning
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string CalculateMD5(Stream s)
        {
            long pos = s.Position;
            s.Position = 0;
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(s);
            s.Position = pos;
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        //        public static void RemoveRunAsAdminXPSP3FromME1()
        //        {
        //#if WINDOWS

        //            string gamePath = GetGamePath(1);
        //            gamePath += "\\Binaries\\MassEffect.exe";
        //            var compatKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);
        //            if (compatKey != null)
        //            {
        //                string compatString = (string)compatKey.GetValue(gamePath, null);
        //                if (compatString != null) //has compat setting
        //                {
        //                    string[] compatsettings = compatString.Split(' ');
        //                    List<string> newSettings = new List<string>();

        //                    foreach (string str in compatsettings)
        //                    {
        //                        switch (str)
        //                        {
        //                            case "~":
        //                            case "RUNASADMIN":
        //                            case "WINXPSP3":
        //                                continue;
        //                            default:
        //                                newSettings.Add(str);
        //                                break;
        //                        }
        //                    }

        //                    if (newSettings.Count > 0)
        //                    {
        //                        string newcompatString = "~";
        //                        foreach (string compatitem in newSettings)
        //                        {
        //                            newcompatString += " " + compatitem;
        //                        }
        //                        if (newcompatString == compatString)
        //                        {
        //                            return;
        //                        }
        //                        else
        //                        {
        //                            compatKey.SetValue(gamePath, newcompatString);
        //                            Log.Information("[AICORE] New stripped compatibility string: " + newcompatString);
        //                        }
        //                    }
        //                    else
        //                    {
        //                        compatKey.DeleteValue(gamePath);
        //                        Log.Information("[AICORE] Removed compatibility settings for ME1.");
        //                    }
        //                }
        //            }
        //#endif
        //        }





        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
        {
            return attributes & ~attributesToRemove;
        }




        public static int RunProcess(string exe, string args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true)
        {
            return RunProcess(exe, null, args, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow);
        }

        public static int RunProcess(string exe, List<string> args, bool waitForProcess = false, bool allowReattemptAsAdmin = false, bool requireAdmin = false, bool noWindow = true)
        {
            return RunProcess(exe, args, null, waitForProcess: waitForProcess, allowReattemptAsAdmin: allowReattemptAsAdmin, requireAdmin: requireAdmin, noWindow: noWindow);
        }

        private static int RunProcess(string exe, List<string> argsL, string argsS, bool waitForProcess, bool allowReattemptAsAdmin, bool requireAdmin, bool noWindow)
        {
            var argsStr = argsS;
            if (argsStr == null && argsL != null)
            {
                argsStr = "";
                foreach (var arg in argsL)
                {
                    if (arg != "") argsStr += " ";
                    if (arg.Contains(" "))
                    {
                        argsStr += $"\"{arg}\"";
                    }
                    else
                    {
                        argsStr += arg;
                    }
                }
            }

            if (requireAdmin)
            {
                Log.Information($"[AICORE] Running process as admin: {exe} {argsStr}");
                //requires elevation
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = exe;
                    p.StartInfo.UseShellExecute = true;
                    p.StartInfo.CreateNoWindow = noWindow;
                    p.StartInfo.Arguments = argsStr;
                    p.StartInfo.Verb = "runas";
                    p.Start();
                    if (waitForProcess)
                    {
                        p.WaitForExit();
                        return p.ExitCode;
                    }

                    return -1;
                }
            }
            else
            {
                Log.Information($"[AICORE] Running process: {exe} {argsStr}");
                try
                {
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = exe;
                        p.StartInfo.UseShellExecute = true;
                        p.StartInfo.CreateNoWindow = noWindow;
                        p.StartInfo.Arguments = argsStr;
                        p.Start();
                        if (waitForProcess)
                        {
                            p.WaitForExit();
                            return p.ExitCode;
                        }

                        return -1;
                    }
                }
                catch (Win32Exception w32e)
                {
                    Log.Warning("[AICORE] Win32 exception running process: " + w32e.ToString());
                    if (w32e.NativeErrorCode == 740 && allowReattemptAsAdmin)
                    {
                        Log.Information("[AICORE] Attempting relaunch with administrative rights.");
                        //requires elevation
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = exe;
                            p.StartInfo.UseShellExecute = true;
                            p.StartInfo.CreateNoWindow = noWindow;
                            p.StartInfo.Arguments = argsStr;
                            p.StartInfo.Verb = "runas";
                            p.Start();
                            if (waitForProcess)
                            {
                                p.WaitForExit();
                                return p.ExitCode;
                            }

                            return -1;
                        }
                    }
                    else
                    {
                        throw w32e; //rethrow to higher.
                    }
                }
            }
        }


        //        internal static void TagWithALOTMarker(string packageFile)
        //        {
        //#if WINDOWS
        //            try
        //            {
        //                using (FileStream fs = new FileStream(packageFile, FileMode.Open, FileAccess.ReadWrite))
        //                {
        //                    fs.SeekEnd();
        //                    fs.Seek(-App.MEMendFileMarker.Length, SeekOrigin.Current);
        //                    string marker = fs.ReadStringASCII(App.MEMendFileMarker.Length);
        //                    if (marker != App.MEMendFileMarker)
        //                    {
        //                        fs.SeekEnd();
        //                        fs.WriteStringASCII(App.MEMendFileMarker);
        //                        Log.Information("[AICORE] Re-tagged file with ALOT Marker");
        //                    }
        //                    else
        //                    {
        //                        Log.Information("[AICORE] File already tagged with ALOT marker, skipping re-tagging");
        //                    }
        //                }
        //            }
        //            catch
        //            {
        //                Log.Error("[AICORE] Failed to tag file with ALOT marker!");
        //            }
        //#endif
        //        }


        public static bool CreateDirectoryWithWritePermission(string directoryPath, bool forcePermissions = false)
        {
            if (!forcePermissions && Directory.Exists(Directory.GetParent(directoryPath).FullName) && Utilities.IsDirectoryWritable(Directory.GetParent(directoryPath).FullName))
            {
                Directory.CreateDirectory(directoryPath);
                return true;
            }

            try
            {
                //try first without admin.
                if (forcePermissions) throw new UnauthorizedAccessException(); //just go to the alternate case.
                Directory.CreateDirectory(directoryPath);
                return true;
            }
            catch (UnauthorizedAccessException uae)
            {
                //Must have admin rights.
                Log.Information("[AICORE] We need admin rights to create this directory");

#if WINDOWS
                string exe = Locations.GetCachedExecutable("PermissionsGranter.exe");
                try
                {
                    Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                }
                catch (Exception e)
                {
                    Log.Error("[AICORE] Error extracting PermissionsGranter.exe: " + e.Message);

                    Log.Information("[AICORE] Retrying with appdata temp directory instead.");
                    try
                    {
                        exe = Path.Combine(Path.GetTempPath(), "PermissionsGranter");
                        Utilities.ExtractInternalFile("MassEffectModManagerCore.modmanager.me3tweaks.PermissionsGranter.exe", exe, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[AICORE] Retry failed! Unable to make this directory writable due to inability to extract PermissionsGranter.exe. Reason: " + ex.Message);
                        return false;
                    }
                }

                string args = "\"" + System.Security.Principal.WindowsIdentity.GetCurrent().Name + "\" -create-directory \"" + directoryPath.TrimEnd('\\') + "\"";
                try
                {
                    int result = Utilities.RunProcess(exe, args, waitForProcess: true, requireAdmin: true, noWindow: true);
                    if (result == 0)
                    {
                        Log.Information("[AICORE] Elevated process returned code 0, restore directory is hopefully writable now.");
                        return true;
                    }
                    else
                    {
                        Log.Error("[AICORE] Elevated process returned code " + result + ", directory likely is not writable");
                        return false;
                    }
                }
                catch (Exception e)
                {
                    if (e is Win32Exception w32e)
                    {
                        if (w32e.NativeErrorCode == 1223)
                        {
                            //Admin canceled.
                            return false;
                        }
                    }

                    Log.Error("[AICORE] Error creating directory with PermissionsGranter: " + e.Message);
                    return false;

                }

#else
                //TODO: UNIX HANDLING OF THIS, SUDO?
                return false;
#endif
            }
        }

        //        public static Task<List<string>> GetArchiveFileListing(string archive)
        //        {
        //#if WINDOWS
        //            string path = MainWindow.BINARY_DIRECTORY + "7z.exe";
        //            string args = "l \"" + archive + "\"";

        //            Log.Information("[AICORE] Running 7z archive inspector process: 7z " + args);
        //            ConsoleApp ca = new ConsoleApp(path, args);
        //            int startindex = 0;
        //            List<string> files = new List<string>();
        //            ca.ConsoleOutput += (o, args2) =>
        //            {
        //                if (startindex < 0)
        //                {
        //                    return;
        //                }
        //                if (args2.Line.Contains("------------------------"))
        //                {
        //                    if (startindex > 0)
        //                    {
        //                        //we found final line
        //                        startindex = -1;
        //                        return;
        //                    }
        //                    startindex = args2.Line.IndexOf("------------------------"); //this is such a hack...
        //                    return;
        //                }
        //                if (startindex > 0)
        //                {
        //                    files.Add(args2.Line.Substring(startindex));
        //                }
        //            };
        //            ca.Run();
        //            ca.WaitForExit();
        //            return Task.FromResult<List<string>>(files);
        //#endif
        //            return Task.FromResult<List<string>>(new List<string>());
        //        }

        public static void TurnOffOriginAutoUpdate()
        {
            Log.Information("[AICORE] Attempting to disable auto update support in Origin");
            string appdatapath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Origin");
            if (Directory.Exists(appdatapath))
            {
                var appdatafiles = Directory.GetFiles(appdatapath, "local_*.xml");
                foreach (string configfile in appdatafiles)
                {
                    try
                    {
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.Load(configfile);

                        XmlNode node = xmlDoc.SelectSingleNode("/Settings/Setting[@key='AutoPatch']");
                        if (node == null)
                        {
                            node = xmlDoc.CreateNode("element", "Setting", "");
                            xmlDoc.SelectSingleNode("/Settings").AppendChild(node);
                        }
                        XmlAttribute attr = xmlDoc.CreateAttribute("type");
                        attr.Value = 1.ToString();
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("value");
                        attr.Value = "false";
                        SetAttrSafe(node, attr);

                        attr = xmlDoc.CreateAttribute("key");
                        attr.Value = "AutoPatch";
                        SetAttrSafe(node, attr);
                        xmlDoc.Save(configfile);
                        Log.Information("[AICORE] Updated file with autopatch off: " + configfile);
                    }
                    catch (Exception e)
                    {
                        Log.Error("[AICORE] Unable to turn off origin in game for file " + configfile + ": " + e.Message);
                    }
                }
            }
            else
            {
                Log.Warning("[AICORE] Origin folder does not exist: " + appdatapath);
            }
        }
        //public static void TurnOffOriginAutoUpdateForGame(int game)
        //{
        //    Log.Information("[AICORE] Attempting to disable auto update support for game: " + game);
        //    string gamePath = GetGamePath(game);
        //    if (gamePath != null && Directory.Exists(gamePath))
        //    {
        //        gamePath += @"\__Installer\installerdata.xml";
        //        if (File.Exists(gamePath))
        //        {
        //            //Origin installer file
        //            string newValue = string.Empty;
        //            XmlDocument xmlDoc = new XmlDocument();

        //            xmlDoc.Load(gamePath);

        //            XmlNode node = xmlDoc.SelectSingleNode("game/metadata/featureFlags");
        //            if (node != null)
        //            {
        //                //set settings same as me3
        //                XmlAttribute attr = xmlDoc.CreateAttribute("autoUpdateEnabled");
        //                attr.Value = 0.ToString();
        //                SetAttrSafe(node, attr);

        //                attr = xmlDoc.CreateAttribute("useGameVersionFromManifestEnabled");
        //                attr.Value = 1.ToString();
        //                SetAttrSafe(node, attr);

        //                attr = xmlDoc.CreateAttribute("treatUpdatesAsMandatory");
        //                attr.Value = 0.ToString();
        //                SetAttrSafe(node, attr);

        //                attr = xmlDoc.CreateAttribute("forceTouchupInstallerAfterUpdate");
        //                attr.Value = 0.ToString();
        //                SetAttrSafe(node, attr);

        //                attr = xmlDoc.CreateAttribute("useGameVersionFromManifestEnabled");
        //                attr.Value = 1.ToString();
        //                SetAttrSafe(node, attr);

        //                attr = xmlDoc.CreateAttribute("enableDifferentialUpdate");
        //                attr.Value = 1.ToString();
        //                SetAttrSafe(node, attr);

        //                xmlDoc.Save(gamePath);
        //            }
        //        }
        //        else
        //        {
        //            Log.Information("[AICORE] Installer manifest does not exist. This does not appear to be an origin installation. Skipping this step");
        //        }
        //    }
        //}

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

        /// <summary>
        /// Gets the amount of installed memory in bytes
        /// </summary>
        /// <returns></returns>
        public static ulong GetInstalledRamAmount()
        {
            var computerInfo = new ComputerInfo();
            return computerInfo.TotalPhysicalMemory;
        }

        public static bool isRunningOnAMD()
        {
            // TODO: MAKE WORK CROSS PLAT
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
            catch (XmlException)
            {
                return false;
            }
        }

        public static string CalculateSHA256(string randomString)
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


        public static long GetSizeOfDirectory(string d, string[] extensionsToCalculate = null)
        {
            return GetSizeOfDirectory(new DirectoryInfo(d), extensionsToCalculate);
        }
        public static long GetSizeOfDirectory(DirectoryInfo d, string[] extensionsToCalculate = null)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                if (extensionsToCalculate != null)
                {
                    if (extensionsToCalculate.Contains(Path.GetExtension(fi.Name)))
                    {
                        size += fi.Length;
                    }
                }
                else
                {
                    size += fi.Length;
                }
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += GetSizeOfDirectory(di, extensionsToCalculate);
            }
            return size;
        }

        public static bool IsSubfolder(string parentPath, string childPath)
        {
            var parentUri = new Uri(parentPath);
            var childUri = new DirectoryInfo(childPath).Parent;
            while (childUri != null)
            {
                if (new Uri(childUri.FullName) == parentUri)
                {
                    return true;
                }
                childUri = childUri.Parent;
            }
            return false;
        }



        /// <summary>
        /// Returns if a game is running or not
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static bool IsGameRunning(Enums.MEGame game)
        {
            if (game == Enums.MEGame.ME1)
            {
                Process[] pname = Process.GetProcessesByName("MassEffect");
                return pname.Length > 0;
            }
            if (game == Enums.MEGame.ME2)
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

        /// <summary>
        /// Returns the running application version information
        /// </summary>
        /// <returns></returns>
        public static Version GetAppVersion() => System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
        
        /// <summary>
        /// Gets the executable path that is hosting this library.
        /// </summary>
        /// <returns></returns>
        public static string GetExecutablePath() => Process.GetCurrentProcess().MainModule.FileName;

        /// <summary>
        /// Gets the version information for the ALOT Installer Core Library.
        /// </summary>
        /// <returns></returns>
        public static Version GetLibraryVersion() => Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        /// Returns the hosting processes' name, without extension
        /// </summary>
        /// <returns></returns>
#if (!WINDOWS && DEBUG)
        // running process will be 'dotnet' in this mode
        public static string GetHostingProcessname() => "ALOTInstallerConsole";
#else
        public static string GetHostingProcessname() => Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.ModuleName);
#endif
        public static void WriteDebugLog(string debugMsg)
        {
            if (Settings.DebugLogs)
            {
                Log.Debug(debugMsg);
            }
        }

        /// <summary>
        /// Gets the name of the installer. e.g. if app is named ALOTInstaller.exe it will return ALOT. If it is MEUITMInstaller it will return MEUITM
        /// </summary>
        /// <returns></returns>
        public static string GetAppPrefixedName()
        {
            var hostingName = Utilities.GetHostingProcessname();
            var installerIndex = hostingName.IndexOf("Installer", StringComparison.InvariantCultureIgnoreCase);
            if (installerIndex > 0)
            {
                return hostingName.Substring(0, installerIndex);
            }

            return "ALOT"; //Default
        }
    }
}