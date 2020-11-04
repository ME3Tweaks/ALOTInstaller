using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using Microsoft.Win32;
using Serilog;

namespace ALOTInstallerCore.Steps
{
    public static class StartupCheck
    {
        /// <summary>
        /// Performs startup checks
        /// </summary>
        /// <param name="messageCallback"></param>
        public static void PerformStartupCheck(Action<string, string> messageCallback)
        {
            PerformRAMCheck(messageCallback);
            PerformWriteCheck(messageCallback, true);

            var textureLibUnavailable = !Settings.TextureLibraryLocationExistedOnLoad &&
                                        Settings.TextureLibrarySettingsLocation != null;
            var stagingDirUnavailable = !Settings.StagingLocationExistedOnLoad &&
                                        Settings.StagingSettingsLocation != null;
            if (textureLibUnavailable || stagingDirUnavailable)
            {
                string title = "";
                if (textureLibUnavailable)
                {
                    title += "Texture library";
                }

                if (stagingDirUnavailable)
                {
                    if (title.Length > 0) title += ", ";
                    title += "Texture staging directory";
                }

                title += " unavailable";

                var message =
                    $"Paths defined in settings were not available when {Utilities.GetAppPrefixedName()} Installer was booted. The below paths are what will be used for this session instead.\n\n" +
                    $"Texture library:\n{Settings.TextureLibraryLocation}\n\n" +
                    $"Texture staging:\n{Settings.BuildLocation}\n\n" +
                    $"You can update the paths where textures are stored before installation (Texture Library) and textures are built for installation (Staging) in the settings.";
                messageCallback?.Invoke(title,message);
            }

        }

        private static void PerformRAMCheck(Action<string, string> messageCallback)
        {
            var ramAmountsBytes = Utilities.GetInstalledRamAmount();
            var installedRamGB = ramAmountsBytes * 1.0d / (2 ^ 30);
            if (ramAmountsBytes > 0 && installedRamGB < 7.98)
            {
                messageCallback?.Invoke("System memory is less than 8 GB", "Building and installing textures uses considerable amounts of memory. Installation will be significantly slower or crash on systems with less than 8 GB of memory. Systems with more than 8GB of memory will see significant speed improvements during installation. During installation ensure you do not have other open processes as the installer will use various amounts of memory at different stages of installation.");
            }
#if WINDOWS
            //Check pagefile
            try
            {
                //Current
                var pageFileLocations = new List<string>();
                using (var query = new ManagementObjectSearcher("SELECT Caption,AllocatedBaseSize FROM Win32_PageFileUsage"))
                {
                    foreach (ManagementBaseObject obj in query.Get())
                    {
                        string pagefileName = (string)obj.GetPropertyValue("Caption");
                        Log.Information("[AICORE] Detected pagefile: " + pagefileName);
                        pageFileLocations.Add(pagefileName.ToLower());
                    }
                }

                //Max
                using (var query = new ManagementObjectSearcher("SELECT Name,MaximumSize FROM Win32_PageFileSetting"))
                {
                    foreach (ManagementBaseObject obj in query.Get())
                    {
                        string pagefileName = (string)obj.GetPropertyValue("Name");
                        uint max = (uint)obj.GetPropertyValue("MaximumSize");
                        if (max > 0)
                        {
                            // Not system managed
                            pageFileLocations.RemoveAll(x => Path.GetFullPath(x).Equals(Path.GetFullPath(pagefileName)));
                            Log.Error($"[AICORE] Pagefile has been modified by the end user. The maximum page file size on {pagefileName} is {max} MB. Does this user **actually** know what capping a pagefile does?");
                        }
                    }
                }

                if (pageFileLocations.Any())
                {
                    Log.Information("[AICORE] We have a usable system managed page file - OK");
                }
                else
                {
                    Log.Error("[AICORE] We have no uncapped or available pagefiles to use! Very high chance application will run out of memory");
                    messageCallback?.Invoke($"Pagefile is off or size has been capped", "The system pagefile (virtual memory) settings are not currently managed by Windows, or the pagefile is off. {Utilities.GetAppPrefixedName()} Installer uses large amounts of memory and will very often run out of memory and crash if virtual memory is capped or turned off. You should not change your pagefile settings.");
                }
            }
            catch (Exception e)
            {
                Log.Error("[AICORE] Unable to check pagefile settings:");
                e.WriteToLog("[AICORE] ");

            }
#endif
        }

        private static bool PerformWriteCheck(Action<string, string> messageCallback, bool required)
        {
#if WINDOWS
            Log.Information("[AICORE] Performing write check on all game directories...");
            var targets = Locations.GetAllAvailableTargets();
            try
            {
                List<string> directoriesToGrant = new List<string>();
                foreach (var t in targets)
                {
                    // Check all folders are writable
                    bool isFullyWritable = true;
                    var testDirectories = Directory.GetDirectories(t.TargetPath, "*", SearchOption.AllDirectories);
                    foreach (var d in testDirectories)
                    {
                        isFullyWritable &= Utilities.IsDirectoryWritable(d);
                    }
                }

                bool isAdmin = Utilities.IsAdministrator();

                if (directoriesToGrant.Any())
                {
                    string args = "";
                    // Some directories not writable
                    foreach (var dir in directoriesToGrant)
                    {
                        if (args != "")
                        {
                            args += " ";
                        }

                        args += $"\"{dir}\"";
                    }

                    args = $"\"{System.Security.Principal.WindowsIdentity.GetCurrent().Name}\" {args}";
                    var permissionsGranterExe = Path.Combine(Locations.ResourcesDir, "Binaries", "PermissionsGranter.exe");

                    //need to run write permissions program
                    if (isAdmin)
                    {
                        int result = Utilities.RunProcess(permissionsGranterExe, args, true, true, true, true);
                        if (result == 0)
                        {
                            Log.Information(
                                "[AICORE] Elevated process returned code 0, directories are hopefully writable now.");
                            return true;
                        }
                        else
                        {
                            Log.Error("[AICORE] Elevated process returned code " + result +
                                      ", directories probably aren't writable.");
                            return false;
                        }
                    }
                    else
                    {
                        string message =
                            $"Some game folders/registry keys are not writeable by your user account. {Utilities.GetAppPrefixedName()} Installer will attempt to grant access to these folders/registry with the PermissionsGranter.exe program:\n";
                        if (required)
                        {
                            message =
                                $"Some game paths and registry keys are not writeable by your user account. These need to be writable or {Utilities.GetAppPrefixedName()} Installer will be unable to install textures. Please grant administrative privileges to PermissionsGranter.exe to give your account the necessary privileges to the following:\n";
                        }

                        foreach (string str in directoriesToGrant)
                        {
                            message += "\n" + str;
                        }

                        messageCallback?.Invoke("Write permissions required for modding", message);
                        int result = Utilities.RunProcess(permissionsGranterExe, args, true, true, true, true);
                        if (result == 0)
                        {
                            Log.Information("[AICORE] Elevated process returned code 0, directories are hopefully writable now.");
                            return true;
                        }
                        else
                        {
                            Log.Error($"[AICORE] Elevated process returned code {result}, directories probably aren't writable.");
                            return false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(
                    "[AICORE] Error checking for write privileges. This may be a significant sign that an installed game is not in a good state.");
                e.WriteToLog("[AICORE] ");
                return false;
            }
#endif
            return true;
        }

#if WINDOWS
        private static void PerformUACCheck()
        {
            bool isAdmin = Utilities.IsAdministrator();

            //Check if UAC is off
            bool uacIsOn = true;
            string softwareKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

            int? value = (int?)Registry.GetValue(softwareKey, "EnableLUA", null);
            if (value != null)
            {
                uacIsOn = value > 0;
                Log.Information("[AICORE] UAC is on: " + uacIsOn);
            }
            if (isAdmin && uacIsOn)
            {
                Log.Warning("[AICORE] This session is running as administrator.");
                //await this.ShowMessageAsync($"{Utilities.GetAppPrefixedName()} Installer should be run as standard user", $"Running {Utilities.GetAppPrefixedName()} Installer as an administrator will disable drag and drop functionality and may cause issues due to the program running in a different user context. You should restart the application without running it as an administrator unless directed by the developers.");
            }
        }
#endif
    }
}
