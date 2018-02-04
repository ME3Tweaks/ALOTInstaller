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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static bool POST_STARTUP = false;

        [STAThread]
        public static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            try
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();
            }
            catch (Exception e)
            {
                OnFatalCrash(e);
                throw e;
            }
        }

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
                        }
                        else
                        {
                            preLogMessages += "Directory doesn't exist for update: " + parsedItems.Value.UpdateDest;
                        }
                    }
                    if (parsedItems.Value.BootingNewUpdate)
                    {
                        if (File.Exists("ALOTAddonBuilder.exe"))
                        {
                            File.Delete("ALOTAddonBuilder.exe");
                        }
                        if (File.Exists("ALOTAddonBuilder.pdb"))
                        {
                            File.Delete("ALOTAddonBuilder.pdb");
                        }
                        if (File.Exists("AlotAddonBuilder.exe.config"))
                        {
                            File.Delete("AlotAddonBuilder.exe.config");
                        }
                    }
                }
            }

            Directory.CreateDirectory(loggingBasePath + "\\logs");
            Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Debug()
                .WriteTo.RollingFile(loggingBasePath + "\\logs\\alotinstaller-{Date}.txt", flushToDiskInterval: new TimeSpan(0, 0, 15))
              .CreateLogger();
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
            POST_STARTUP = true;
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
            Log.Information("Working directory: " + Directory.GetCurrentDirectory());

            //Update Mode
            if (updateDestinationPath != null)
            {
                Thread.Sleep(2000); //SLEEP WHILE WE WAIT FOR PARENT PROCESS TO STOP.
                Log.Information("In update mode. Update destination: " + updateDestinationPath);
                Log.Information("Applying update");
                CopyDir.CopyAll(new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory), new DirectoryInfo(updateDestinationPath));
                Log.Information("Update files have been applied.");
                updateDestinationPath += "\\"; //add slash
                Log.Information("Performing update migrations...");
                if (Directory.Exists(updateDestinationPath + "MEM_Packages") && !Directory.Exists(updateDestinationPath + @"Data\MEM_Packages"))
                {
                    Log.Information("Migrating MEM_Packages folder into subfolder");
                    Directory.Move(updateDestinationPath + "MEM_Packages", updateDestinationPath + @"Data\MEM_Packages");
                }

                if (Directory.Exists(updateDestinationPath + "music") && !Directory.Exists(updateDestinationPath + @"Data\Music"))
                {
                    Log.Information("Migrating music folder into subfolder");
                    Directory.Move(updateDestinationPath + "music", updateDestinationPath + @"Data\Music");
                }

                if (Directory.Exists(updateDestinationPath + "bin"))
                {
                    Log.Information("Deleting old top level bin folder: " + (updateDestinationPath + "bin"));
                    Utilities.DeleteFilesAndFoldersRecursively(updateDestinationPath + "bin");
                }

                if (Directory.Exists(updateDestinationPath + "lib"))
                {
                    Log.Information("Deleting old top level lib folder");
                    Utilities.DeleteFilesAndFoldersRecursively(updateDestinationPath + "lib");
                }

                if (Directory.Exists(updateDestinationPath + "Extracted_Mods"))
                {
                    Log.Information("Deleting leftover Extracted_Mods folder");
                    Utilities.DeleteFilesAndFoldersRecursively(updateDestinationPath + "Extracted_Mods");
                }

                if (File.Exists(updateDestinationPath + "manifest.xml"))
                {
                    Log.Information("Deleting leftover manifest.xml file");
                    File.Delete(updateDestinationPath + "manifest.xml");
                }

                if (File.Exists(updateDestinationPath + "ALOTInstaller.exe.config"))
                {
                    Log.Information("Deleting leftover config file");
                    File.Delete(updateDestinationPath + "ALOTInstaller.exe.config");
                }

                if (File.Exists(updateDestinationPath + "manifest-bundled.xml"))
                {
                    Log.Information("Deleting leftover manifest-bundled.xml file");
                    File.Delete(updateDestinationPath + "manifest-bundled.xml");
                }

                if (File.Exists(updateDestinationPath + "DEV_MODE"))
                {
                    Log.Information("Pulling application out of developer mode");
                    File.Delete(updateDestinationPath + "DEV_MODE");
                }
                Log.Information("Rebooting into normal mode to complete update: " + updateDestinationPath + System.AppDomain.CurrentDomain.FriendlyName);
                ProcessStartInfo psi = new ProcessStartInfo(updateDestinationPath + System.AppDomain.CurrentDomain.FriendlyName);
                psi.WorkingDirectory = updateDestinationPath;
                psi.Arguments = "--completing-update";
                Process.Start(psi);
                Environment.Exit(0);
                System.Windows.Application.Current.Shutdown();
            }

            //Normal Mode
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(UIElement),
            new FrameworkPropertyMetadata(15000));
            if (Directory.Exists(loggingBasePath + "Update"))
            {
                Thread.Sleep(1000);
                Log.Information("Removing Update directory");
                Directory.Delete(loggingBasePath + "Update", true);
            }
            if (File.Exists(loggingBasePath + "ALOTAddonBuilder.exe"))
            {
                Log.Information("Deleting Update Shim ALOTAddonBuilder.exe");
                File.Delete(loggingBasePath + "ALOTAddonBuilder.exe");
            }
            Log.Information("Program Version: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
            Log.Information("System information:\n" + Utilities.GetOperatingSystemInfo());
            string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString();
            Log.Information("Running Windows " + releaseId);
            Utilities.GetAntivirusInfo();
        }

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("ALOT Installer has crashed! This exception that caused the crash:");
            string st = FlattenException(e.Exception);
            Log.Error(errorMessage);
            Log.Error(st);
            Log.Information("Forcing beta mode off");
            Utilities.WriteRegistryKey(Registry.CurrentUser, AlotAddOnGUI.MainWindow.REGISTRY_KEY, AlotAddOnGUI.MainWindow.SETTINGSTR_BETAMODE, 0);

            if (Directory.Exists("Data"))
            {
                File.Create(@"Data\APP_CRASH");
            }
            //MetroDial.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //e.Handled = true;
        }

        public static void OnFatalCrash(Exception e)
        {
            if (!POST_STARTUP)
            {
                string errorMessage = string.Format("ALOT Installer has encountered a serious fatal startup crash:\n" + FlattenException(e));
                File.WriteAllText("FATAL_STARTUP_CRASH.txt", errorMessage);
            }
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
            var exists = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1;
            if (!exists)
            {
                Log.Information("Only instance running, killing other apps...");
                Utilities.runProcess("cmd.exe", "/c taskkill /F /IM MassEffectModderNoGui.exe /T", true);
                Utilities.runProcess("cmd.exe", "/c taskkill /F /IM 7z.exe /T", true);
            }
            Log.Information("Closing application via AppClosing()");
        }

        private static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var probingPath = AppDomain.CurrentDomain.BaseDirectory + @"Data\lib";
            var assyName = new AssemblyName(args.Name);

            var newPath = Path.Combine(probingPath, assyName.Name);
            if (!newPath.EndsWith(".dll"))
            {
                newPath = newPath + ".dll";
            }
            if (File.Exists(newPath))
            {
                var assy = Assembly.LoadFile(newPath);
                return assy;
            }

            return null;
        }
    }

    class Options
    {
        [Option('u', "update-dest",
          HelpText = "Copies AddonBuilder and everything in the current directory (and subdirectories) into the listed directory, then reboots using the new EXE.")]
        public string UpdateDest { get; set; }

        [Option('c', "completing-update",
            HelpText = "Indicates that we are booting a new copy of ALOTInstaller that has just been upgraded")]
        public bool BootingNewUpdate { get; set; }
    }

}
