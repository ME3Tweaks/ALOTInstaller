using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App() : base()
        {
            Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
                  .WriteTo.LiterateConsole()
                .WriteTo.RollingFile("logs\\alotaddoninstaller-{Date}.txt")
              .CreateLogger();
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
            Log.Information("=====================================================");
            Log.Information("Logger Started for ALOT Installer.");
            Log.Information("Program Version: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
            Log.Information("System information:\n"+Utilities.GetOperatingSystemInfo());
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("An unhandled application exception occurred. This exception is not being handled, only logged: {0}", e.Exception.Message);
            string st = e.ToString();
            Log.Error(errorMessage);
            Log.Error(st);
            //MetroDial.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //e.Handled = true;
        }
    }

}
