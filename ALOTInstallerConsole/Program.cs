using System;
using System.IO;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
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
            startupUI.BeginFlow();
            Application.Run(startupUI);

        }

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
    }
}
