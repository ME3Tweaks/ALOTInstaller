#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.PlatformSpecific.Windows;
using Microsoft.Win32;
using Serilog;

namespace ALOTInstallerCore
{
    // WINDOWS SPECIFIC ITEMS IN UTILITIES

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
        public static void RemoveAppCompatForME1Path(GameTarget me1Target)
        {
            var exePath = MEDirectories.ExecutablePath(me1Target);
            if (RegistryHandler.DeleteRegistryValue(Registry.CurrentUser, @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", exePath))
            {
                CoreAnalytics.TrackEvent?.Invoke("Removed appcompat settings from ME1", null);
                Log.Information("[AICORE] Removed compatibility settings for ME1.");
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
                uint productVal = (uint) productState;
                var bytes = BitConverter.GetBytes(productVal);
                Log.Information("[AICORE] Antivirus info: " + virusCheckerName + " with state " + bytes[1].ToString("X2") + " " + bytes[2].ToString("X2") + " " + bytes[3].ToString("X2"));
            }
        }


        [DllImport("kernel32.dll")]
        static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

        /// <summary>
        /// Creates a link from sourceFile (which is where the fake file is), pointing to the target file.
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetFile"></param>
        /// <returns></returns>
        public static bool WinCreateFileSymbolicLink(string sourceFile, string targetFile)
        {
            if (File.Exists(sourceFile))
            {
                Log.Error($"Cannot create symlink from disk location that already has file: {sourceFile}");
            }

            if (!File.Exists(targetFile))
            {
                Log.Error($@"Cannot create symlink to file that doesn't exist: {targetFile}");
            }

            try
            {
                // Apparently this only works if you're running as admin or in developer mode
                // Because of some really bad design decisions at microsoft with UAC
                return CreateSymbolicLink(sourceFile, targetFile, SymbolicLink.File);
            }
            catch (Exception e)
            {
                Log.Warning($@"Cannot create symbolic link from {sourceFile} to {targetFile}: {e.Message}");
            }

            return false;
        }
    }
}
#endif
