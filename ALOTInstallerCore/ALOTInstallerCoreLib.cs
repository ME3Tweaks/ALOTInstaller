using System;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
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
        /// <param name="setCallingLoggerCallback">Function to pass this library's loger back</param>
        public static void Startup(Action<ILogger> setCallingLoggerCallback)
        {
            if (startedUp) return;
            startedUp = true;
            LogCollector.SetWrapperLogger = setCallingLoggerCallback;
            setCallingLoggerCallback?.Invoke(LogCollector.CreateLogger());
            Settings.Load();
            Locations.LoadLocations();
        }
    }
}
