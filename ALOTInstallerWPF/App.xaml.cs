using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using CommandLine;
using Serilog;

namespace ALOTInstallerWPF
{

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static bool BetaAvailable { get; set; }
        public static bool CheckedForMEMUpdates { get; set; }


#if DEBUG
        public static Visibility DebugModeVisibility => Visibility.Visible;
#else
    public static Visibility DebugModeVisibility => Visibility.Collapsed;
#endif
        public App() : base()
        {
            handleCommandLine();
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(UIElement),
                new FrameworkPropertyMetadata(15000));
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
                typeof(Control),
                new FrameworkPropertyMetadata(true));
        }

        private void handleCommandLine()
        {
            #region Command line
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var result = Parser.Default.ParseArguments<Options>(args);
                if (result is Parsed<Options> parsedCommandLineArgs)
                {
                    //Parsing completed
                    if (parsedCommandLineArgs.Value.UpdateBoot)
                    {
                        //Update unpacked and process was run.
                        // Exit the process as we have completed the extraction process for single file .net core
                        Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                        return;
                    }

                    if (parsedCommandLineArgs.Value.UpdateRebootDest != null)
                    {
                        Log.Logger = LogCollector.CreateLogger();
                        Log.Information(LogCollector.SessionStartString);
                        copyAndRebootUpdate(parsedCommandLineArgs.Value.UpdateRebootDest);
                        return;
                    }

                    if (parsedCommandLineArgs.Value.UpdateRebootDestDir != null)
                    {
                        Log.Logger = LogCollector.CreateLogger();
                        Log.Information(LogCollector.SessionStartString);
                        copyAndRebootUpdateV3(parsedCommandLineArgs.Value.UpdateRebootDestDir);
                        return;
                    }
                }
                else
                {
                    Log.Error("Could not parse command line arguments! Args: " + string.Join(' ', args));
                }
            }

            #endregion

        }

        /// <summary>
        /// Upgrade from V3 update and swap
        /// </summary>
        /// <param name="updateRebootDestDir"></param>
        private void copyAndRebootUpdateV3(string updateRebootDestDir)
        {
            Thread.Sleep(2000); //SLEEP WHILE WE WAIT FOR PARENT PROCESS TO STOP.
            Log.Information("In update mode. Update destination: " + updateRebootDestDir);
            int i = 0;
            var targetFile = Path.Combine(updateRebootDestDir, "ALOTInstaller.exe");
            while (i < 5)
            {
                i++;
                try
                {
                    Log.Information("Applying update");
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Copy(Utilities.GetExecutablePath(), targetFile);
                    ProcessStartInfo psi = new ProcessStartInfo(targetFile)
                    {
                        WorkingDirectory = updateRebootDestDir
                    };
                    Process.Start(psi);
                    Environment.Exit(0);
                    break;
                }
                catch (Exception e)
                {
                    Log.Error("Error applying update: " + e.Message);
                    if (i < 5)
                    {
                        Thread.Sleep(1000);
                        Log.Information("Attempt #" + (i + 1));
                    }
                    else
                    {
                        Log.Fatal("Unable to apply update after 5 attempts. We are giving up.");
                        MessageBox.Show($"Update was unable to apply. The last error message was {e.Message}.\nSee the logs directory in {LogCollector.LogDir} for more information.\n\nUpdate file: {Utilities.GetExecutablePath()}\nDestination file: {targetFile}\n\nIf this continues to happen please come to the ALOT discord or download a new release from GitHub.");
                        Environment.Exit(1);
                    }
                }
            }
        }

        /// <summary>
        /// V4 update reboot and swap
        /// </summary>
        /// <param name="updateRebootDest"></param>
        private void copyAndRebootUpdate(string updateRebootDest)
        {
            Thread.Sleep(2000); //SLEEP WHILE WE WAIT FOR PARENT PROCESS TO STOP.
            Log.Information("In update mode. Update destination: " + updateRebootDest);
            int i = 0;
            while (i < 5)
            {
                i++;
                try
                {
                    Log.Information("Applying update");
                    if (File.Exists(updateRebootDest)) File.Delete(updateRebootDest);
                    File.Copy(Utilities.GetExecutablePath(), updateRebootDest);
                    ProcessStartInfo psi = new ProcessStartInfo(updateRebootDest)
                    {
                        WorkingDirectory = Directory.GetParent(updateRebootDest).FullName
                    };
                    Process.Start(psi);
                    Environment.Exit(0);
                    break;
                }
                catch (Exception e)
                {
                    Log.Error("Error applying update: " + e.Message);
                    if (i < 5)
                    {
                        Thread.Sleep(1000);
                        Log.Information("Attempt #" + (i + 1));
                    }
                    else
                    {
                        Log.Fatal("Unable to apply update after 5 attempts. We are giving up.");
                        MessageBox.Show($"Update was unable to apply. The last error message was {e.Message}.\nSee the logs directory in {LogCollector.LogDir} for more information.\n\nUpdate file: {Utilities.GetExecutablePath()}\nDestination file: {updateRebootDest}\n\nIf this continues to happen please come to the ALOT discord or download a new release from GitHub.");
                        Environment.Exit(1);
                    }
                }
            }
        }

        class Options
        {
            [Option("update-dest",
                HelpText = "Legacy update flag for upgrading from ALOT Installer V3. This is the directory that this executable should be copied to, and booted from.")]
            public string UpdateRebootDestDir { get; private set; }

            [Option("update-dest-path",
                HelpText = "Copies this program's executable to the specified location, runs the new executable, and then exits this process.")]
            public string UpdateRebootDest { get; private set; }

            [Option("me1path",
                HelpText = "Sets the path for Mass Effect on app boot. It must point to the game root directory.")]
            public bool PassthroughME1Path { get; private set; }

            [Option("me2path",
                HelpText = "Sets the path for Mass Effect 2 on app boot. It must point to the game root directory.")]
            public bool PassthroughME2Path { get; private set; }

            [Option("me3path",
                HelpText = "Sets the path for Mass Effect 3 on app boot. It must point to the game root directory.")]
            public bool PassthroughME3Path { get; private set; }

            [Option("update-boot",
                HelpText = "Indicates that the process should run in update mode for a single file .net core executable. The process will exit upon starting because the platform extraction process will have completed.")]
            public bool UpdateBoot { get; private set; }

        }
    }
}
