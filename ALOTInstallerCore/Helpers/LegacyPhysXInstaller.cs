using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ME3ExplorerCore.Helpers;
using Microsoft.Win32;
using Serilog;
#if WINDOWS
using ALOTInstallerCore.PlatformSpecific.Windows;
#endif

namespace ALOTInstallerCore.Helpers
{
    public class LegacyPhysXInstaller
    {
        public static bool IsPhysxKeyWritable()
        {
#if WINDOWS
            var ageiaKey = @"SOFTWARE\WOW6432Node\AGEIA Technologies";
            return RegistryHandler.TestKeyWritable(Registry.LocalMachine, ageiaKey);
#else
            return true;
#endif
        }

        public static bool IsLegacyPhysXInstalled()
        {
#if WINDOWS
            // Guidance from mirh https://github.com/ME3Tweaks/ALOTInstaller/issues/23
            var me1LegacyPhysxEngineVal = RegistryHandler.GetRegistryInt(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\AGEIA Technologies\PhysX_A32_Engines", "2.7.2");
            if (me1LegacyPhysxEngineVal.HasValue && me1LegacyPhysxEngineVal.Value != -1)
            {
                var legacyPhysXEngineFolder = RegistryHandler.GetRegistryString(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\AGEIA Technologies", "PhysXCore Path");
                if (legacyPhysXEngineFolder != null)
                {
                    var legacyPhysx = Path.Combine(legacyPhysXEngineFolder, "v2.7.2", "PhysXCore.dll");
                    return Directory.Exists(legacyPhysXEngineFolder) && File.Exists(legacyPhysx);
                }
            }

            return false; //Registry check failed
#else
            // Linux users should know what they're doing
            return true;
#endif
        }

        /// <summary>
        /// Patches the ME1 PhysxLoader to always use local. This will make it so ME1 will run regardless if it can access the registry or not with the enableLocalPhysXCore key.
        /// </summary>
        /// <param name="me1Target"></param>
        /// <returns></returns>
        public static bool PatchPhysXLoaderME1(GameTarget me1Target)
        {
            Log.Information("[AICORE] Patching PhysXLoader.dll to force loading local PhysXCore");
            var loaderPath = Path.Combine(me1Target.TargetPath, "Binaries", "PhysXLoader.dll");
            if (File.Exists(loaderPath) && new FileInfo(loaderPath).Length == 68688) //Make sure it's same size so it's not like some other build
            {
                using var pls = File.Open(loaderPath, FileMode.Open, FileAccess.ReadWrite);
                pls.Seek(0x1688, SeekOrigin.Begin);

                var jzByte1 = pls.ReadByte();
                var jzByte2 = pls.ReadByte();
                Log.Information($@"[AICORE] Byte 1 @ 0x1688: 0x{jzByte1:X2}");
                Log.Information($@"[AICORE] Byte 2 @ 0x1689: 0x{jzByte2:X2}");
                if (jzByte1 == 0x75 && jzByte2 == 0x19)
                {
                    // It's a jz instruction. Change to nop
                    Log.Information(@"[AICORE] This file is has the original PhysXLoader.dll jump instruction for allowing system PhysX. Patching out to force local PhysX codepath");
                    pls.Seek(-2, SeekOrigin.Current);
                    pls.WriteByte(0x90); //nop
                    pls.WriteByte(0x90); //nop
                    Log.Information(@"[AICORE] PhysXLoader.dll has been patched");
                }
                else if (jzByte1 == 0x90 && jzByte2 == 0x90)
                {
                    Log.Information(@"[AICORE] This file appears to have already been patched to force local use of PhysX. Not patching file.");
                }
                else
                {
                    Log.Warning(@"[AICORE] Bytes are not expected values. We will not patch this file.");
                    return false;
                }
            }
            return true;
        }

        public static async Task<string> InstallLegacyPhysX(Action<long, long> processCallback, Action<string> setStatusCallback, Func<string, string, string, string, bool> confirmDialogCallback, Action<string, string> errorDialogCallback, CancellationTokenSource cancellationTokenSource)
        {
            Log.Information("[AICORE] Downloading Legacy PhysX redistributable.");
            var redistMD5 = "f1187d700ab88f7d6fe243caa89025dd";
            var legacyRedistLink = "https://github.com/ME3Tweaks/ALOTInstaller/releases/download/4.0.684.1951/PhysXRedist.zip";

            //var downloadResult = new MemoryStream(File.ReadAllBytes(@"E:\Documents\Visual Studio 2015\Projects\AlotAddOnGUI\PhysXRedist\PhysXRedist.zip"));


            // Warn user first
            var acceptedWarning = confirmDialogCallback?.Invoke("Important information about PhysX + Mass Effect",
                "ALOT Installer is going to make Mass Effect use the system wide version of legacy PhysX, rather than the local one it has. This is due to Mass Effect setting a registry key that breaks other games that depend on the same version of PhysX, and also makes it require administrative rights. Due to multiple complex issues with modified executables running as administrator, this fix is required for installation of textures to ensure the game will run.\n\nOnce this change is made IT IS VITAL THAT YOU DO NOT UNINSTALL LEGACY PHYSX - ALL COPIES OF MASS EFFECT, VANILLA OR MODDED, WILL DEPEND ON THIS LEGACY PHYSX BEING INSTALLED FROM NOW ON.", "Accept", "Abort install");
            if (acceptedWarning.HasValue && acceptedWarning.Value)
            {
                var downloadResult = await OnlineContent.DownloadToMemory(legacyRedistLink, processCallback, redistMD5, cancellationTokenSource: cancellationTokenSource);
                if (downloadResult.errorMessage == null)
                {
                    // OK
                    // Open the archive.
                    //using var zip = new ZipArchive(downloadResult, ZipArchiveMode.Read);
                    using var zip = new ZipArchive(downloadResult.result, ZipArchiveMode.Read);
                    var licenseEntry = zip.Entries.FirstOrDefault(x => Path.GetFileName(x.FullName) == "license.txt");
                    if (licenseEntry != null)
                    {

                        var licenseText = new StreamReader(licenseEntry.Open()).ReadToEnd();
                        Log.Information(@"[AICORE] Prompting user to access PhysX license");
                        var acceptedLicense = confirmDialogCallback?.Invoke("You must accept the Legacy PhysX license to continue", licenseText, "Accept", "Decline");
                        if (acceptedLicense.HasValue && acceptedLicense.Value)
                        {
                            Log.Information(@"[AICORE] License accepted by user for PhysX");

                            // msi payload
                            if (!zip.Entries.Any(x => Path.GetFileName(x.FullName) == "install.cmd"))
                            {
                                return "The installer script was not found in the download. Contact the developers to get this fixed."; // How could this be fixed if md5 was correct??
                            }

                            // Run install.cmd payload
                            setStatusCallback?.Invoke("Preparing Legacy PhysX installer");
                            var legacyInstallDir = Path.Combine(Locations.TempDirectory(), "LegacyPhysXInstaller");
                            Directory.CreateDirectory(legacyInstallDir);
                            zip.ExtractToDirectory(legacyInstallDir);

                            try
                            {
                                setStatusCallback?.Invoke("Installing Legacy PhysX");
                                var returnCode = Utilities.RunProcess(@"cmd.exe", $"/c \"{Path.Combine(legacyInstallDir, "install.cmd")}", true,
                                    true, true, true, true);
                                CoreAnalytics.TrackEvent?.Invoke("Converted ME1 from local PhysX to system PhysX", new Dictionary<string, string>()
                                {
                                    {"Result code", returnCode.ToString()}
                                });

                                if (returnCode == 0)
                                {
                                    Log.Information(@"[AICORE] Conversion from local PhysX to system PhysX for ME1 successful.");
                                    return null;
                                }

                                return $"Installer script returned error code {returnCode} while installing PhysX.";
                            }
                            catch (Win32Exception w32e)
                            {
                                Log.Error($@"[AICORE] Error (Win32) running physx installer as admin: {w32e.Message}");
                                return "Installation of Legacy PhysX was aborted.";
                                // Return something? Maybe handle exception specifically
                            }
                            catch (Exception e)
                            {
                                Log.Error($@"[AICORE] Error running PhysX installer as admin: {e.Message}");
                                return $"An error occurred installing Legacy PhysX: {e.Message}";
                            }
                            finally
                            {
                                try
                                {
                                    var installLogPath = Path.Combine(legacyInstallDir, "install.log");
                                    if (File.Exists(installLogPath))
                                    {
                                        var loglines = File.ReadLines(installLogPath);
                                        Log.Information(@"[AICORE] Installation log for PhysX Redistributable:");
                                        foreach (var line in loglines)
                                        {
                                            Log.Information($@"[AICORE]    {line}");
                                        }
                                    }

                                    Utilities.DeleteFilesAndFoldersRecursively(legacyInstallDir);
                                }
                                catch
                                {
                                } //Don't care about the exception.
                            }
                        }
                    }
                    else
                    {
                        Log.Error(@"[AICORE] license.txt file not found in zip archive! Cannot show the required license before install");
                        return "The license file was not found in the downloaded archive.";
                    }
                }
                else
                {
                    Log.Error($"[AICORE] Error downloading the Legacy PhysX redistributable: {downloadResult.errorMessage}");
                    return $"Downloading the Legacy PhysX installer failed: {downloadResult.errorMessage}. Download the package from {legacyRedistLink}, extract the package, and run install.cmd as administrator to manually apply the fixes.";
                }
            }
            else
            {
                Log.Error(@"[AICORE] User declined the the additional information prompt about switching Mass Effect onto system Legacy PhysX");
                return "Cannot install textures while Mass Effect is using the game's local PhysX instance. To ensure the game works properly after texture modding, it must use the system's Legacy PhysX installation.";
            }

            return "An unknown error has occurred.";

        }
    }
}