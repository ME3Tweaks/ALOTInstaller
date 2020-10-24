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
using ME3ExplorerCore.Helpers;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    public class LegacyPhysXInstaller
    {
        public static bool IsLegacyPhysXInstalled()
        {
#if !WINDOWS
            // Linux users should know what they're doing
            return true;
#else
            // Probably should look in the registry, this is kind of a hack
            var legacyPhysXEngineFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"NVIDIA Corporation\PhysX\Engine\v2.7.2");
            var legacyPhysx = Path.Combine(legacyPhysXEngineFolder, "PhysXCore.dll");
            return Directory.Exists(legacyPhysXEngineFolder) && File.Exists(legacyPhysx);
#endif
        }

        public static async Task<string> InstallLegacyPhysX(Action<long, long> processCallback, Action<string> setStatusCallback, Func<string, string, string, string, bool> confirmDialogCallback, Action<string, string> errorDialogCallback, CancellationTokenSource cancellationTokenSource)
        {
            Log.Information("[AICORE] Downloading Legacy PhysX redistributable.");
            var redistMD5 = "6b5c95b4c649c69b32d073cdca33fb1a";
            var downloadResult = await OnlineContent.DownloadToMemory("https://github.com/ME3Tweaks/ALOTInstaller/raw/ALOT-v4/PhysXRedist/PhysXRedist.zip", processCallback, redistMD5, cancellationTokenSource: cancellationTokenSource); // ALOT-V4 branch must not be removed
            if (downloadResult.errorMessage == null)
            {
                // OK
                // Open the archive.
                using var zip = new ZipArchive(downloadResult.result, ZipArchiveMode.Read);
                var licenseEntry = zip.Entries.FirstOrDefault(x => Path.GetFileName(x.FullName) == "license.txt");
                if (licenseEntry != null)
                {
                    var licenseText = new StreamReader(licenseEntry.Open()).ReadToEnd();
                    Log.Information(@"[AICORE] Prompting user to access PhysX license");
                    var acceptedLicense = confirmDialogCallback?.Invoke("You must accept the Legacy PhysX license to continue", licenseText, "Accept", "Decline");
                    if (acceptedLicense != null && acceptedLicense.Value)
                    {
                        Log.Information(@"[AICORE] License accepted by user for PhysX");
                        // Install PhysX

                        // msi payload
                        var msiStream = zip.Entries.FirstOrDefault(x => Path.GetFileName(x.FullName) == "physxLegacy.msi");
                        if (msiStream == null)
                        {
                            return
                                "The PhysX installer was not found in the download. Contact the developers to get this fixed."; // How could this be fixed if md5 was correct??
                        }

                        var msiPath = Path.Combine(Locations.TempDirectory(), "LegacyPhysXInstaller.msi");
                        var memStream = new MemoryStream();
                        msiStream.Open().CopyTo(memStream); //can't copy directly
                        memStream.WriteToFile(msiPath);
                        try
                        {
                            setStatusCallback?.Invoke("Installing Legacy PhysX");
                            var returnCode = Utilities.RunProcess(@"msiexec.exe", $"/i \"{msiPath}\" /passive", true,
                                true, true, false);
                            if (returnCode == 0)
                            {
                                return null;
                            }

                            return $"MSIExec returned error code {returnCode} while installing PhysX.";
                        }
                        catch (Win32Exception w32e)
                        {
                            Log.Error($@"[AICORE] Error (Win32) running physx installer as admin: {w32e.Message}");
                            return "Installation of Legacy PhysX was aborted.";
                            // Return something? Maybe handle exception specifically
                        }
                        catch (Exception e)
                        {
                            Log.Error($@"[AICORE] Error running physx installer as admin: {e.Message}");
                            return $"An error occurred installing Legacy PhysX: {e.Message}";
                        }
                        finally
                        {
                            try
                            {
                                File.Delete(msiPath);
                            } catch { } //Don't care about the exception.
                        }

                        return null;
                    }
                    else
                    {
                        Log.Error(@"[AICORE] User declined the license agreement for Legacy PhysX");
                        return "The license agreement for Legacy PhysX must be accepted to install the software.";
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
                return downloadResult.errorMessage;
            }

            return "An unknown error has occurred.";
        }
    }
}
