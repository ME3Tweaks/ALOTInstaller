using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using ALOTInstallerWPF.Telemetry;
using ControlzEx.Theming;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MassEffectModManagerCore.modmanager.asi;
using ME3ExplorerCore.Compression;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;

namespace ALOTInstallerWPF.BuilderUI
{
    public class StartupUIController
    {
        private static void SetWrapperLogger(ILogger logger) => Log.Logger = logger;

        private static void startTelemetry()
        {
            initAppCenter();
            AppCenter.SetEnabledAsync(true);
        }

        private static void stopTelemetry()
        {
            AppCenter.SetEnabledAsync(false);
        }

        private static bool telemetryStarted = false;

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
            var pd = await window.ShowProgressAsync("Starting up", $"{Utilities.GetAppPrefixedName()} Installer is starting up. Please wait.");
            pd.SetIndeterminate();
            NamedBackgroundWorker bw = new NamedBackgroundWorker("StartupThread");
            bw.DoWork += (a, b) =>
            {
                ALOTInstallerCoreLib.Startup(SetWrapperLogger, RunOnUIThread, startTelemetry, stopTelemetry);
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
                AppUpdater.PerformGithubAppUpdateCheck("Mgamerz", "ALOTInstallerTest", "ALOTInstallerWPF", "ALOTInstallerWPF.exe",
                    (title, text, updateButtonText, declineButtonText) =>
                    {
                        bool response = false;
                        object syncObj = new object();
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                var result = await mw.ShowMessageAsync(title, text, MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
                                {
                                    AffirmativeButtonText = updateButtonText,
                                    NegativeButtonText = declineButtonText,
                                    DefaultButtonFocus = MessageDialogResult.Affirmative
                                });
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
                    }, (title, initialmessage, canCancel) =>
                    {
                        // We don't use this as we are already in a progress dialog
                        pd.SetCancelable(canCancel);
                        pd.SetMessage(initialmessage);
                        pd.SetTitle(title);
                    },
                    s =>
                    {
                        pd.SetMessage(s);
                    },
                    (done, total) =>
                    {
                        pd.SetProgress(done * 1d / total);
                        pd.SetMessage($"Downloading update {FileSizeFormatter.FormatSize(done)} / {FileSizeFormatter.FormatSize(total)}");
                    },
                    () =>
                    {
                        pd.SetIndeterminate();
                    },
                    (title, message) =>
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
                    () =>
                    {
                        App.BetaAvailable = true;
                    },
                    ct
                );

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
                    // ?? What do we do here.
                }


                pd.SetMessage("Checking for MassEffectModderNoGui updates");
                MEMUpdater.UpdateMEM(downloadProgressChanged, errorUpdating, setStatus);

                pd.SetMessage("Loading installer framework");
                ALOTInstallerCoreLib.PostCriticalStartup(x => pd.SetMessage(x), RunOnUIThread);

                BackupService.RefreshBackupStatus(Locations.GetAllAvailableTargets(), false);

                pd.SetMessage("Loading installer manifests");
                var alotManifestModePackage = ManifestHandler.LoadMasterManifest(x => pd.SetMessage(x));

                
                void setStatus(string message)
                {
                    pd.SetIndeterminate();
                    pd.SetMessage(message);
                }

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

                pd.SetMessage("Performing startup checks");
                StartupCheck.PerformStartupCheck((title, message) =>
                {
                    object o = new object();
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        if (Application.Current.MainWindow is MainWindow mw)
                        {
                            await mw.ShowMessageAsync(title, message);
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
                });


                pd.SetMessage("Preparing interface");
                Thread.Sleep(250); // This will allow this message to show up for moment so user can see it.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.Title = $"{Utilities.GetAppPrefixedName()} Installer {Utilities.GetAppVersion()}";
                        mw.ContentGrid.Children.Add(new FileSelectionUIController());
                        mw.SettingsFlyoutControl.Content = mw.SettingsFlyoutContent = new SettingsFlyout();
                        mw.DiagnosticsFlyoutControl.Content = new DiagnosticsFlyout();
                        mw.FileImporterFlyoutContent = new FileImporterFlyout();
                        mw.LODSwitcherFlyout.Content = mw.LODSwitcherFlyoutContent = new LODSwitcherFlyout();
                    }
                });
            };
            bw.RunWorkerCompleted += async (a, b) =>
                    {
                        await pd.CloseAsync();
#if !DEBUG
                        await window.ShowMessageAsync("This is a preview version of ALOT Installer V4",
                            "This is a preview version of ALOT Installer V4. Changes this program makes to you texture library will make those files incompatible with ALOT Installer V3. Please direct all feedback to the #v4-feedback channel on the ALOT Discord. Thanks!");
#endif
                    };
            bw.RunWorkerAsync();


            return;
        }

        private static void RunOnUIThread(Action obj)
        {
            Application.Current.Dispatcher.Invoke(obj);
        }
    }
}