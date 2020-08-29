using System;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Services;
using Serilog;

namespace ALOTInstallerCore
{
    /// <summary>
    /// Class that is used to setup the library
    /// </summary>
    public static class ALOTInstallerCoreLib
    {
        private static bool startedUp;

        /// <summary>
        /// Starts the library initialization. This method must ALWAYS be called before using the library. This will load the settings, setup telemetry, start the logger, load locations, and begin the backup service.
        /// </summary>
        /// <param name="setCallingLoggerCallback">Function to pass this library's logger back</param>
        /// <param name="runOnUiThreadCallback">Callback that contains method that should be wrapped in a UI-thread only runner. Some object initialization can only be performed on the UI thread</param>
        public static void Startup(Action<ILogger> setCallingLoggerCallback, Action<Action> runOnUiThreadCallback, Action startTelemetryCallback = null, Action stopTelemetryCallback = null)
        {
            if (startedUp) return;
            startedUp = true;
            CoreAnalytics.StartTelemetryCallback = startTelemetryCallback;
            CoreAnalytics.StopTelemetryCallback = stopTelemetryCallback;
            LogCollector.SetWrapperLogger = setCallingLoggerCallback;
            setCallingLoggerCallback?.Invoke(LogCollector.CreateLogger());
            Log.Information("============================SESSION START============================");
            Log.Information("[AICORE] ALOTInstallerCore library is booting");
            Log.Information("[AICORE] Loading settings");

            Settings.Load();
            if (Settings.Telemetry)
            {
                Log.Information("[AICORE] Telemetry callback being invoked (if any is set)");
                startTelemetryCallback?.Invoke();
            }
            Log.Information("[AICORE] Loading targets and locations");

            Locations.LoadLocations();
            Log.Information("[AICORE] Starting backup service");

            BackupService.InitBackupService(runOnUiThreadCallback);

        }
    }
}
