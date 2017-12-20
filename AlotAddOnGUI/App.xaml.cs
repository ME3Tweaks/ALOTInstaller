using AlotAddOnGUI.classes;
using CommandLine;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
            string[] args = Environment.GetCommandLineArgs();
            string preLogMessages = "";
            Parsed<Options> parsedItems = null;
            string loggingBasePath = System.AppDomain.CurrentDomain.BaseDirectory;
            string updateDestinationPath = null;
            if (args.Length > 1)
            {
                var result = Parser.Default.ParseArguments<Options>(args);
                if (result.GetType() == typeof(Parsed<Options>))
                {
                    //Parsing succeeded - have to do update check to keep logs in order...
                    parsedItems = (Parsed<Options>)result;
                    if (parsedItems.Value.UpdateDest != null)
                    {
                        if (Directory.Exists(parsedItems.Value.UpdateDest))
                        {
                            updateDestinationPath = parsedItems.Value.UpdateDest;
                            loggingBasePath = updateDestinationPath;
                        } else
                        {
                            preLogMessages += "Directory doesn't exist for update: " + parsedItems.Value.UpdateDest;
                        }
                    }
                }
            }

            Directory.CreateDirectory(loggingBasePath + "\\logs");
            Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
                .WriteTo.RollingFile(loggingBasePath + "\\logs\\alotaddoninstaller-{Date}.txt", flushToDiskInterval: new TimeSpan(0, 0, 15))
              .CreateLogger();
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
            Log.Information("=====================================================");
            Log.Information("Logger Started for ALOT Installer.");
            if (preLogMessages != "")
            {
                Log.Information("Prelogger messages: " + preLogMessages);
            }
            if (args.Length > 0)
            {
                string commandlineargs = "";
                for (int i = 0; i < args.Length; i++)
                {
                    commandlineargs += args[i] + " ";
                }
                Log.Information("Command line arguments: " + commandlineargs);
            }
            //Update Mode
            if (updateDestinationPath != null)
            {
                Thread.Sleep(2000); //SLEEP WHILE WE WAIT FOR PARENT PROCESS TO STOP.
                Log.Information("In update mode. Update destination: " + updateDestinationPath);
                Log.Information("Applying update");
                CopyDir.CopyAll(new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory), new DirectoryInfo(updateDestinationPath));
                Log.Information("Files copied - rebooting into normal mode");
                ProcessStartInfo psi = new ProcessStartInfo(updateDestinationPath + "\\" + System.AppDomain.CurrentDomain.FriendlyName);
                psi.WorkingDirectory = updateDestinationPath;
                Process.Start(psi);
                Environment.Exit(0);
                System.Windows.Application.Current.Shutdown();
            }

            //Normal Mode
            if (Directory.Exists(loggingBasePath + "Update"))
            {
                Thread.Sleep(1000);
                Log.Information("Removing Update directory");
                Directory.Delete(loggingBasePath + "Update", true);
            }
            Log.Information("Program Version: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
            Log.Information("System information:\n" + Utilities.GetOperatingSystemInfo());
            string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
            Log.Information("Running Windows " + releaseId);
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("ALOT Addon GUI has encountered an uncaught error! This exception is not being handled, only logged for debugging:");
            string st = FlattenException(e.Exception);
            Log.Error(errorMessage);
            Log.Error(st);
            Log.Information("Forcing beta mode off");
            Utilities.WriteRegistryKey(Registry.CurrentUser, AlotAddOnGUI.MainWindow.REGISTRY_KEY, AlotAddOnGUI.MainWindow.SETTINGSTR_BETAMODE , 0);
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

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            Utilities.runProcess("cmd.exe", "/c taskkill /F /IM MassEffectModderNoGui.exe /T", true);
            Utilities.runProcess("cmd.exe", "/c taskkill /F /IM 7z.exe /T", true);
            Log.Information("Closing application via AppClosing()");
        }
    }

    class Options
    {
        [Option('u', "update-dest",
          HelpText = "Copies AddonBuilder and everything in the current directory (and subdirectories) into the listed directory, then reboots using the new EXE.")]
        public string UpdateDest { get; set; }

    }

}
