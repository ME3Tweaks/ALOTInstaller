using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.ModManager.Objects;
using Microsoft.Win32;
using Serilog;

namespace ALOTInstallerCore
{
    // WINDOWS SPECIFIC ITEMS IN UTILITIES
#if WINDOWS
    public static partial class Utilities
    {

        public const int WIN32_EXCEPTION_ELEVATED_CODE = -98763;
        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Removes the app compat flags for this specific target. We do not want system wide as it will make game not launch
        /// if it is not patched to support local physx
        /// </summary>
        /// <param name="me1Target"></param>
        public static void RemoveRunAsAdminXPSP3FromME1(GameTarget me1Target)
        {
            var exePath = MEDirectories.ExecutablePath(me1Target);
            Log.Information($@"[AICORE] Removing app compat flags for ME1 executable at {exePath}. Remove ~, RUNASADMIN, WINXPSP3");
            try
            {
                var compatKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);
                string compatString = (string) compatKey?.GetValue(exePath, null);
                if (compatString != null) //has compat setting
                {
                    string[] compatsettings = compatString.Split(' ');
                    List<string> newSettings = new List<string>();

                    foreach (string str in compatsettings)
                    {
                        switch (str)
                        {
                            // Don't add these to the new settings.
                            case "~": // we will add this back if there's anything to add
                            case "RUNASADMIN":
                            case "WINXPSP3":
                                continue;
                            default:
                                newSettings.Add(str);
                                break;
                        }
                    }

                    if (newSettings.Count > 0)
                    {
                        string newcompatString = "~";
                        foreach (var compatitem in newSettings)
                        {
                            newcompatString += $" {compatitem}";
                        }

                        if (newcompatString == compatString)
                        {
                            Log.Information($"[AICORE] No compat flags needed updated for ME1");
                            return; //No changes
                        }
                        else
                        {
                            compatKey.SetValue(exePath, newcompatString);
                            Log.Information($"[AICORE] New stripped compatibility string for ME1: {newcompatString}");
                        }
                    }
                    else
                    {
                        compatKey.DeleteValue(exePath);
                        CoreAnalytics.TrackEvent?.Invoke("Removed appcompat settings from ME1", null);
                        Log.Information("[AICORE] Removed compatibility settings for ME1.");
                    }
                }
                else
                {
                    Log.Information($@"[AICORE] No app compat flags found for this executable");
                }
            }
            catch (Exception e)
            {
                Log.Error($@"[AICORE] Error removing app compat flags: {e.Message}");
            }
        }

        public static bool GrantAccess(string fullPath)
        {
            try
            {
                DirectoryInfo dInfo = new DirectoryInfo(fullPath);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                dSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                dInfo.SetAccessControl(dSecurity);
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Error granting write access: " + e.Message);
                return false;
            }
            return true;
        }

        public static bool MakeAllFilesInDirReadWrite(string directory)
        {
            Log.Information("[AICORE] Marking all files in directory to read-write: " + directory);
            //Log.Warning("[AICORE] If the application crashes after this statement, please come to to the ALOT discord - this is an issue we have not yet been able to reproduce and thus can't fix without outside assistance.");
            var di = new DirectoryInfo(directory);
            foreach (var file in di.GetFiles("*", SearchOption.AllDirectories))
            {
                if (!file.Exists)
                {
                    Log.Warning("[AICORE] File is no longer in the game directory: " + file.FullName);
                    Log.Error("[AICORE] This file was found when the read-write filescan took place, but is no longer present. The application is going to crash.");
                    Log.Error("[AICORE] Another session may be running, or there may be a bug. Please come to the ALOT Discord so we can analyze this as we cannot reproduce it.");
                }

                //Utilities.WriteDebugLog("Clearing read-only marker (if any) on file: " + file.FullName);
                try
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
                catch (DirectoryNotFoundException ex)
                {
                    if (file.FullName.Length > 260)
                    {
                        Log.Error("[AICORE] Path of file is too long - Windows API length limitations for files will cause errors. File: " + file.FullName);
                        Log.Error("[AICORE] The game is either nested too deep or a mod has been improperly installed causing a filepath to be too long.");
                        ex.WriteToLog("[AICORE] ");
                    }
                    return false;
                }
            }
            return true;
        }

        public static bool OpenAndSelectFileInExplorer(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return false;
            }
            //Clean up file path so it can be navigated OK
            filePath = System.IO.Path.GetFullPath(filePath);
            System.Diagnostics.Process.Start("explorer.exe", string.Format("/select,\"{0}\"", filePath));
            return true;
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
                Log.Information("[AICORE] Antivirus info: " + virusCheckerName + " with state " + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + " " + bytes[3].ToString("X2"));
            }
        }
    }
#endif

}
