using System;
using System.IO;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Services;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.asi;
using NickStrupat;
using Serilog;

namespace ALOTInstallerCore
{
    /// <summary>
    /// Class that is used to setup the library
    /// </summary>
    public static class ALOTInstallerCoreLib
    {
        private static bool startedUp;

#if WINDOWS
        public static Version MIN_SUPPORTED_WINDOWS_OS = new Version("6.3.9600"); //Windows 8.1 Update 3
#endif

        /// <summary>
        /// Starts the library initialization. This method must ALWAYS be called before using the library. This will initialize telemetry, load settings, cleanup the temp directory, and more.
        /// </summary>
        /// <param name="setCallingLoggerCallback">Function to pass this library's logger back</param>
        /// <param name="runOnUiThreadCallback">Callback that contains method that should be wrapped in a UI-thread only runner. Some object initialization can only be performed on the UI thread</param>
        public static void Startup(Action<ILogger> setCallingLoggerCallback, Action<Action> runOnUiThreadCallback, Action startTelemetryCallback = null, Action stopTelemetryCallback = null, string firstLogMessage = null, bool loadSettingsFolders = true)
        {
            if (startedUp) return;
            startedUp = true;
            CoreAnalytics.StartTelemetryCallback = startTelemetryCallback;
            CoreAnalytics.StopTelemetryCallback = stopTelemetryCallback;
            LogCollector.SetWrapperLogger = setCallingLoggerCallback;
            setCallingLoggerCallback?.Invoke(LogCollector.CreateLogger());
            Log.Information(LogCollector.SessionStartString);
            if (firstLogMessage != null)
            {
                Log.Information($@"[AICORE] {firstLogMessage}");
            }
            Log.Information("[AICORE] ALOTInstallerCore library is booting");
            Log.Information($"[AICORE] Library version: {Utilities.GetLibraryVersion()}");

            try
            {
                ComputerInfo ci = new ComputerInfo();
                Log.Information(@"[AICORE] System information:");
                Log.Information($@"[AICORE]     Operating system: {ci.OSFullName}");
                Log.Information($@"[AICORE]     Processor:        {ci.CPUName}");
                Log.Information($@"[AICORE]     Memory:           {FileSize.FormatSize(ci.TotalPhysicalMemory)}");
            }
            catch (Exception e)
            {
                Log.Error($@"[AICORE] Error getting startup system info: {e.Message}");
            }

            Log.Information("[AICORE] Loading settings");
            Settings.Load(loadSettingsFolders);
            if (Settings.Telemetry)
            {
                Log.Information("[AICORE] Telemetry callback being invoked (if any is set)");
                startTelemetryCallback?.Invoke();
            }

            // Cleanup lib temp
            if (Directory.Exists(Locations.TempDirectory()))
            {
                Log.Information(@"[AICORE] Deleting existing temp directory");
                try
                {
                    Utilities.DeleteFilesAndFoldersRecursively(Locations.TempDirectory());
                }
                catch (Exception e)
                {
                    Log.Error($@"[AICORE] Failed to cleanup Temp directory in appdata: {e.Message}");
                }
            }
        }

        /// <summary>
        /// This is a continuation of the startup for the library. This method should be called after Startup() and any critical work is done, such as checkingk for updates. The
        /// rest of the library should not load until the update check is done as it may be the source of a crash the update is designed to fix
        /// </summary>
        public static void PostCriticalStartup(Action<string> currentOperationCallback, Action<Action> runOnUiThreadCallback, bool loadTargets = true)
        {
            // Load ME3ExplorerCore library
            Log.Information(@"[AICORE] Loading ME3ExplorerCore library");
            LegendaryExplorerCoreLib.InitLib(LegendaryExplorerCoreLib.SYNCHRONIZATION_CONTEXT, x => { Log.Error($"Error saving package: {x}"); });

            // Logs call in method
            if (loadTargets)
                Locations.LoadTargets();
            Log.Information("[AICORE] Starting backup service");
            BackupService.InitBackupService(runOnUiThreadCallback);

            currentOperationCallback?.Invoke("Loading ME3Tweaks services");
            Log.Information("[AICORE] Loading ME3Tweaks service: Basegame File Identification Service (BGFIS)");

            var willcheckforupdates = OnlineContent.CanFetchContentThrottleCheck();
            BasegameFileIdentificationService.LoadService();

            Log.Information("[AICORE] Loading ME3Tweaks service: Third Party Mod Identification Service (TPMI)");
            ThirdPartyIdentificationService.ModDatabase = OnlineContent.FetchThirdPartyIdentificationManifest();
            ASIManager.LoadManifest();

            if (willcheckforupdates)
            {
                Settings.LastContentCheck = DateTime.Now;
            }

            Log.Information(@"[AICORE] Starting periodic refresh");
            PeriodicRefresh.StartPeriodicRefresh();
        }
    }
}
