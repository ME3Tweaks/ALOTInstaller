using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
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
                .WriteTo.RollingFile("logs\\alotaddoninstaller-{Date}.txt")
              .CreateLogger();
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
            Log.Information("=====================================================");
            Log.Information("Logger Started for ALOT Installer.");
            Log.Information("Program Version: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
            Log.Information("System information:\n" + Utilities.GetOperatingSystemInfo());
            string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
            Log.Information("Running Windows "+releaseId);
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("ALOT Addon GUI has encountered an uncaught error! This exception is not being handled, only logged for debugging:");
            string st = FlattenException(e.Exception);
            Log.Error(errorMessage);
            Log.Error(st);
            //MetroDial.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //e.Handled = true;
        }

        public static string FlattenException(Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }
    }

}
