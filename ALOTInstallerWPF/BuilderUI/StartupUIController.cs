using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.PlatformSpecific.Windows;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Telemetry;
using ControlzEx.Theming;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Win32;
using NickStrupat;
using Serilog;
using Utilities = ALOTInstallerCore.Utilities;

namespace ALOTInstallerWPF.BuilderUI
{
    public class StartupUIController
    {
        private static void SetWrapperLogger(ILogger logger) => Log.Logger = logger;
        private static bool telemetryStarted = false;
        internal static string PassthroughME1Path;
        internal static string PassthroughME2Path;
        internal static string PassthroughME3Path;

        private static void startTelemetry()
        {
            initAppCenter();
            AppCenter.SetEnabledAsync(true);
        }

        private static void stopTelemetry()
        {
            AppCenter.SetEnabledAsync(false);
        }

        private static void initAppCenter()
        {
#if !DEBUG
            if (APIKeys.HasAppCenterKey && !telemetryStarted)
            {
                Microsoft.AppCenter.Crashes.Crashes.GetErrorAttachments = (ErrorReport report) =>
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    // Attach some text.
                    string errorMessage = "ALOT Installer has crashed! This is the exception that caused the crash:\n" + report.StackTrace;
                    Log.Fatal(errorMessage);
                    Log.Error("Note that this exception may appear to occur in a follow up boot due to how appcenter works");
                    string log = LogCollector.CollectLatestLog(false);
                    if (log.Length < 1024 * 1024 * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "crashlog.txt"));
                    }
                    else
                    {
                        //Compress log
                        var compressedLog = LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(log));
                        attachments.Add(ErrorAttachmentLog.AttachmentWithBinary(compressedLog, "crashlog.txt.lzma", "application/x-lzma"));
                    }

                    // Attach binary data.
                    //var fakeImage = System.Text.Encoding.Default.GetBytes("Fake image");
                    //ErrorAttachmentLog binaryLog = ErrorAttachmentLog.AttachmentWithBinary(fakeImage, "ic_launcher.jpeg", "image/jpeg");

                    return attachments;
                };
                AppCenter.Start(APIKeys.AppCenterKey, typeof(Analytics), typeof(Crashes));
            }
#else
            if (!APIKeys.HasAppCenterKey)
            {
                Debug.WriteLine(" >>> This build is missing an API key for AppCenter!");
            }
            else
            {
                Debug.WriteLine("This build has an API key for AppCenter");
            }
#endif
            telemetryStarted = true;
        }

        public static async void BeginFlow(MetroWindow window)
        {
            try
            {
                // This is in a try catch because this is a critical no-crash zone that is before launch
                window.Title = $"{Utilities.GetAppPrefixedName()} Installer {Utilities.GetAppVersion()}";
            }
            catch { }

            if (Utilities.GetExecutablePath().StartsWith(Path.GetTempPath(), StringComparison.InvariantCultureIgnoreCase))
            {
                // Running from temp! This is not allowed
                await window.ShowMessageAsync("Cannot run from temp directory", $"{Utilities.GetAppPrefixedName()} Installer cannot be run from the system's Temp directory. If this executable was run from within an archive, it needs to be extracted first.");
                Environment.Exit(1);
            }

            var pd = await window.ShowProgressAsync("Starting up", $"{Utilities.GetAppPrefixedName()} Installer is starting up. Please wait.");
            pd.SetIndeterminate();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupThread");
            bw.DoWork += (a, b) =>
            {
                RegistryHandler.RegistrySettingsPath = @"HKEY_CURRENT_USER\Software\ALOTAddon";
                ALOTInstallerCoreLib.Startup(SetWrapperLogger, RunOnUIThread, startTelemetry, stopTelemetry);
                // Logger is now available

                // Setup telemetry handlers
                CoreAnalytics.TrackEvent = TelemetryController.TrackEvent;
                CoreCrashes.TrackError = TelemetryController.TrackError;
                CoreCrashes.TrackError2 = TelemetryController.TrackError2;
                CoreCrashes.TrackError3 = TelemetryController.TrackError3;
                if (Settings.BetaMode)
                {
                    RunOnUIThread(() =>
                    {
                        ThemeManager.Current.ChangeTheme(App.Current, "Dark.Red");
                    });
                }

                pd.SetMessage("Checking for application updates");
                CancellationTokenSource ct = new CancellationTokenSource();
                pd.Canceled += (sender, args) =>
                {
                    ct.Cancel();
                };
                AppUpdateInteropPackage uip = new AppUpdateInteropPackage()
                {
                    GithubOwner = "ME3Tweaks",
                    GithubReponame = "ALOTInstaller",
                    UpdateAssetPrefix = "ALOTInstallerWPF",
                    UpdateFilenameInArchive = "ALOTInstaller.exe",
                    ShowUpdatePromptCallback = (title, text, updateButtonText, declineButtonText) =>
                    {
                        bool response = false;
                        object syncObj = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                var result = await mw.ShowMessageAsync(title, text,
                                    MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
                                    {
                                        AffirmativeButtonText = updateButtonText,
                                        NegativeButtonText = declineButtonText,
                                        DefaultButtonFocus = MessageDialogResult.Affirmative
                                    },
                                    75);
                                response = result == MessageDialogResult.Affirmative;
                                lock (syncObj)
                                {
                                    Monitor.Pulse(syncObj);
                                }
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }

                        return response;
                    },
                    ShowUpdateProgressDialogCallback = (title, initialmessage, canCancel) =>
                    {
                        // We don't use this as we are already in a progress dialog
                        pd.SetCancelable(canCancel);
                        pd.SetMessage(initialmessage);
                        pd.SetTitle(title);
                    },
                    ShowMessageCallback = (title, message) =>
                    {
                        object syncObj = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                await mw.ShowMessageAsync(title, message);
                                lock (syncObj)
                                {
                                    Monitor.Pulse(syncObj);
                                }
                            }
                        });
                        lock (syncObj)
                        {
                            Monitor.Wait(syncObj);
                        }
                    },
                    SetUpdateDialogTextCallback = s =>
                    {
                        pd.SetMessage(s);
                    },
                    NotifyBetaAvailable = () => App.BetaAvailable = true,
                    ProgressIndeterminateCallback = () => pd.SetIndeterminate(),
                    ProgressCallback = (done, total) =>
                    {
                        pd.SetProgress(done * 1d / total);
                        pd.SetMessage($"Downloading update {FileSize.FormatSize(done)} / {FileSize.FormatSize(total)}");
                    },
                    DownloadCompleted = () =>
                    {
                        pd.SetCancelable(false);
                    },
                    cancellationTokenSource = ct,
                    ForcedUpgradeMaxReleaseAge = 5,
                    ApplicationName = Utilities.GetAppPrefixedName(),
                    RequestHeader = "ALOTInstallerWPF"
                };

                
                AppUpdater.PerformGithubAppUpdateCheck(uip);

                // If user aborts download
                pd.SetCancelable(false);
                pd.SetIndeterminate();
                pd.SetTitle("Starting up");

                void downloadProgressChanged(long bytes, long total)
                {
                    //Log.Information("Download: "+bytes);
                    pd.SetMessage($"Updating MassEffectModderNoGui {bytes * 100 / total}%");
                    pd.SetProgress(bytes * 1.0d / total);
                }

                void errorUpdating(Exception e)
                {
                    Log.Error($@"[AIWPF] Error updating MEM: {e.Message}");
                    // ?? What do we do here.
                }

                void setStatus(string message)
                {
                    pd.SetIndeterminate();
                    pd.SetMessage(message);
                }

                try
                {
                    pd.SetMessage("Loading installer manifests");
                    var alotManifestModePackage = ManifestHandler.LoadMasterManifest(x => pd.SetMessage(x));

                    b.Result = alotManifestModePackage;
                    if (ManifestHandler.MasterManifest != null)
                    {
                        ManifestHandler.SetCurrentMode(ManifestHandler.GetDefaultMode());
                        pd.SetMessage("Preparing texture library");
                        foreach (var v in ManifestHandler.MasterManifest.ManifestModePackageMappping)
                        {
                            TextureLibrary.ResetAllReadyStatuses(ManifestHandler.GetManifestFilesForMode(v.Key));
                        }
                    }
                    else
                    {
                        // This shouldn't happen...
                    }

                    pd.SetMessage("Checking for MassEffectModderNoGui updates");
                    MEMUpdater.UpdateMEM(downloadProgressChanged, errorUpdating, setStatus);

                    // Must come after MEM update check to help ensure we have MEM available
                    pd.SetMessage("Loading installer framework");
                    ALOTInstallerCoreLib.PostCriticalStartup(x => pd.SetMessage(x), RunOnUIThread);
                    handleM3Passthrough();

                    pd.SetMessage("Performing startup checks");
                    StartupCheck.PerformStartupCheck((title, message) =>
                    {
                        object o = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                await mw.ShowMessageAsync(title, message, ContentWidthPercent: 75);
                                lock (o)
                                {
                                    Monitor.Pulse(o);
                                }
                            }
                        });
                        lock (o)
                        {
                            Monitor.Wait(o);
                        }
                    }, x => pd.SetMessage(x));


                }
                catch (Exception e)
                {
                    Log.Error(@"[AIWPF] There was an error starting up the installer!");
                    e.WriteToLog("[AIWPF] ");
                }

                pd.SetMessage("Preparing interface");
                var hasWorkingMEM = MEMIPCHandler.TestWorkingMEM();

                Thread.Sleep(250); // This will allow this message to show up for moment so user can see it.
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.Title = $"{Utilities.GetAppPrefixedName()} Installer {Utilities.GetAppVersion()}";
                        mw.ContentGrid.Children.Add(new FileSelectionUIController());
                        mw.SettingsFlyoutControl.Content = mw.SettingsFlyoutContent = new SettingsFlyout();
                        mw.DiagnosticsFlyoutControl.Content = new DiagnosticsFlyout();
                        mw.FileImporterFlyoutContent = new FileImporterFlyout();
                        mw.LODSwitcherFlyout.Content = mw.LODSwitcherFlyoutContent = new LODSwitcherFlyout();
                        if (!hasWorkingMEM)
                        {
                            await mw.ShowMessageAsync("Required components are not available",
                                "Some components for installation are not available, likely due to network issues (blocking, no internet, etc). To install these components, folow the 'How to install the Installer Support Package' directions on any of the ALOT pages on NexusMods. The installer will not work without these files installed.",
                                ContentWidthPercent: 75);
                        }
                    }


                });
            };
            bw.RunWorkerCompleted += async (a, b) =>
                        {
                            if (ManifestHandler.MasterManifest != null)
                            {
                                if (ManifestHandler.MasterManifest.Source < ManifestHandler.ManifestSource.Online)
                                {
                                    window.Title += $" - Using {ManifestHandler.MasterManifest.Source} manifest";
                                }
                                else if (ManifestHandler.MasterManifest.Source == ManifestHandler.ManifestSource.Failover)
                                {
                                    window.Title += " - FAILED TO LOAD MANIFEST";
                                }
                            }

                            await pd.CloseAsync();
#if !DEBUG
                        //await window.ShowMessageAsync("This is a preview version of ALOT Installer V4",
                        //    "This is a preview version of ALOT Installer V4. Changes this program makes to you texture library will make those files incompatible with ALOT Installer V3. Please direct all feedback to the #v4-feedback channel on the ALOT Discord. Thanks!");
#endif
                    };
            bw.RunWorkerAsync();


            return;
        }

        private static void handleM3Passthrough()
        {
            if (PassthroughME1Path != null) handlePassthrough(MEGame.ME1, PassthroughME1Path);
            if (PassthroughME2Path != null) handlePassthrough(MEGame.ME2, PassthroughME2Path);
            if (PassthroughME3Path != null) handlePassthrough(MEGame.ME3, PassthroughME3Path);

            PassthroughME1Path = PassthroughME2Path = PassthroughME3Path = null;

            void handlePassthrough(MEGame game, string path)
            {
                if (path != null && Directory.Exists(path))
                {
                    GameTarget gt = new GameTarget(game, path, true, false);
                    var passThroughValidationResult = gt.ValidateTarget(false);
                    if (passThroughValidationResult != null)
                    {
                        Log.Error($@"[AIWPF] {game} path passthrough failed game target validation: {passThroughValidationResult}");
                    }
                    else
                    {
                        Log.Information($@"[AIWPF] Valid passthrough for game {game}. Assigning path.");
                        MEMIPCHandler.SetGamePath(game, path);
                    }
                }
            }
        }

        private static void RunOnUIThread(Action obj)
        {
            Application.Current.Dispatcher.Invoke(obj);
        }
    }
}