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
        /// Starts the library initialization. This method must ALWAYS be called before using the library.
        /// </summary>
        /// <param name="setCallingLoggerCallback">Function to pass this library's logger back</param>
        /// <param name="runOnUiThreadCallback">Callback that contains method that should be wrapped in a UI-thread only runner. Some object initialization can only be performed on the UI thread</param>
        public static void Startup(Action<ILogger> setCallingLoggerCallback, Action<Action> runOnUiThreadCallback)
        {
            if (startedUp) return;
            startedUp = true;
            LogCollector.SetWrapperLogger = setCallingLoggerCallback;
            setCallingLoggerCallback?.Invoke(LogCollector.CreateLogger());
            Settings.Load();
            Locations.LoadLocations();
            BackupService.InitBackupService(runOnUiThreadCallback);
        }
    }
}
