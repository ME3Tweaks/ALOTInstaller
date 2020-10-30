using System;
using System.Diagnostics;
using System.Threading;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    class Program
    {
        private static void setWrapperLogger(ILogger logger) => Log.Logger = logger;
        static void Main(string[] args)
        {
            Locations.AppDataFolderName = "ALOTInstallerConsole"; // Do not change this!
            var bufferHeight = Console.BufferHeight;
            try
            {
                Application.Init();
                var sc = new SynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(sc);

                //Initialize ALOT Installer library
                ALOTInstallerCoreLib.Startup(setWrapperLogger, action => { });

                var startupUI = new BuilderUI.StartupUIController();
                ViewLoop(startupUI);
            }
            catch (Exception e)
            {
                // Unhandled exception!
                try
                {
                    Console.BufferHeight = bufferHeight; //Restore
                }
                catch { } //Can't restore console height on platform.
                Console.Error.WriteLine(e.FlattenWithTrace());
            }
        }


        private static void ViewLoop(UIController initialController)
        {
            _nextUIController = initialController;
            while (true)
            {
                SetNextView();
            }
        }

        /// <summary>
        /// Swaps the current top level UIController (if any) with another one.
        /// </summary>
        /// <param name="controller"></param>
        public static void SetNextView()
        {
            _nextUIController.SetupUI();
            _nextUIController.BeginFlow();
            _currentController = _nextUIController;
            _nextUIController = null;
            Application.Run(_currentController);
        }

        private static UIController _nextUIController;

        private static UIController _currentController;
        /// <summary>
        /// Swaps the current top level UIController (if any) with another one.
        /// </summary>
        /// <param name="controller"></param>
        public static void SwapToNewView(UIController controller)
        {
            Application.MainLoop.Invoke(() =>
            {
                _nextUIController = controller;
                _currentController?.SignalStopping();
                Application.RequestStop();
                Debug.WriteLine($"Stopped a view. The new one is now {Application.Top}");
            });
        }

    }
}
