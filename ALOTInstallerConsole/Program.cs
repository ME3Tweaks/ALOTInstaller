using System;
using System.Collections.Generic;
using System.IO;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Startup;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            SetupLogger();
#if WINDOWS
Hook.

#endif

            Application.Init();
            ManifestModes[OnlineContent.ManifestMode.None] = new OnlineContent.ManifestPackage(); //blank
            var startupUI = new BuilderUI.StartupUIController();
            startupUI.SetupUI();
            startupUI.BeginFlow();
            Application.Run(startupUI);
        }


        public static Dictionary<OnlineContent.ManifestMode, OnlineContent.ManifestPackage> ManifestModes = new Dictionary<OnlineContent.ManifestMode, OnlineContent.ManifestPackage>();

        /// <summary>
        /// Sets up the logger for this application as well as the core library
        /// </summary>
        private static void SetupLogger()
        {
            var logsDir = Directory.CreateDirectory(Path.Combine(Locations.AppDataFolder(), "Logs")).FullName;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.RollingFile(Path.Combine(logsDir, "alotinstallerconsole-{Date}.txt"), flushToDiskInterval: new TimeSpan(0, 0, 15))
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();
            Hook.SetLogger(Log.Logger);
        }

        public static void SwapToNewView(UIController controller)
        {
            Application.RequestStop();
            Application.Run(controller);
        }

        public static OnlineContent.ManifestPackage CurrentManifestPackage;

    }
}
