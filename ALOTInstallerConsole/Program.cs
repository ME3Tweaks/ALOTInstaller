using System;
using System.Collections.Generic;
using System.IO;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects.Manifest;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            SetupLogger();
            Application.Init();
            var startupUI = new BuilderUI.StartupUIController();
            startupUI.SetupUI();
            Program.SwapToNewView(startupUI);
        }

        /// <summary>
        /// Sets up the logger for this application as well as the core library
        /// </summary>
        private static void SetupLogger()
        {
            var logsDir = Directory.CreateDirectory(Path.Combine(Locations.AppDataFolder(), "Logs")).FullName;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logsDir, "alotinstallerconsole-{Date}.txt"), rollingInterval: RollingInterval.Day, flushToDiskInterval: new TimeSpan(0, 0, 15))
#if DEBUG
                .WriteTo.Debug()
#endif
                .CreateLogger();
            Hook.SetLogger(Log.Logger);
        }

        /// <summary>
        /// Swaps the current top level UIController (if any) with another one.
        /// </summary>
        /// <param name="controller"></param>
        public static void SwapToNewView(UIController controller)
        {
            controller.SignalStopping();
            Application.RequestStop();
            controller.BeginFlow();
            Application.Run(controller);
        }
    }
}
