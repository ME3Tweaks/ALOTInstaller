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
using System.Xml.Linq;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerWPF.BuilderUI;
using ALOTInstallerWPF.InstallerUI;
using CommandLine;
using LegendaryExplorerCore.Misc;
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
            //debug();
            Locations.AppDataFolderName = "ALOTInstallerWPF"; // Do not change this!
            handleCommandLine();
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(UIElement),
                new FrameworkPropertyMetadata(15000));
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
                typeof(Control),
                new FrameworkPropertyMetadata(true));
        }

        private void debug()
        {
#if DEBUG
            var meuitmBasepath = @"X:\MEUITM2\";
            var meuitmIni = Path.Combine(meuitmBasepath, "installer.ini");
            var meuitmModsDir = Path.Combine(meuitmBasepath, "Mods");
            DuplicatingIni ini = DuplicatingIni.LoadIni(meuitmIni);

            XElement root = new XElement("root");
            foreach (var v in ini.Sections)
            {
                if (v.Header.StartsWith("Mod"))
                {
                    XElement cf = new XElement("choicefile");
                    cf.SetAttributeValue("choicetitle", $"{v.Entries.FirstOrDefault(x => x.Key == "Label1")?.Value} textures");
                    cf.SetAttributeValue("defaultselectedindex", "0");
                    int i = 1;
                    while (true)
                    {
                        var fileX = v.Entries.FirstOrDefault(x => x.Key == $"File{i}")?.Value;
                        var labelX = v.Entries.FirstOrDefault(x => x.Key == $"Label{i}")?.Value;

                        if (fileX != null && labelX != null)
                        {
                            if (labelX == "Dont install")
                            {
                                cf.SetAttributeValue("allownoinstall", "true");
                                i++;
                                continue;
                            }
                            // it's file
                            XElement pfce = new XElement("packagefile");
                            pfce.SetAttributeValue("me2", "true");
                            pfce.SetAttributeValue("movedirectly", "true");
                            var subPath = Utilities.GetRelativePath(Path.Combine(meuitmModsDir, fileX), meuitmBasepath);
                            pfce.SetAttributeValue("sourcename", subPath);
                            pfce.SetAttributeValue("choicetitle", labelX);
                            cf.Add(pfce);
                        }
                        else
                        {
                            break;
                        }
                        i++;
                    }
                    root.Add(cf);
                }
            }
            Debug.WriteLine(root);
#endif
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Log.Information(@"[AIWPF] Application is exiting. Killing any remaining MassEffectModderNoGui processes");
                MEMIPCHandler.KillAllActiveMEMInstances();
                Log.Information(@"[AIWPF] Stopping application");
            }
            finally
            {
                base.OnExit(e);
            }
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

                    if (parsedCommandLineArgs.Value.PassthroughME1Path != null)
                    {
                        StartupUIController.PassthroughME1Path = parsedCommandLineArgs.Value.PassthroughME1Path;
                    }
                    if (parsedCommandLineArgs.Value.PassthroughME2Path != null)
                    {
                        StartupUIController.PassthroughME2Path = parsedCommandLineArgs.Value.PassthroughME2Path;
                    }
                    if (parsedCommandLineArgs.Value.PassthroughME3Path != null)
                    {
                        StartupUIController.PassthroughME3Path = parsedCommandLineArgs.Value.PassthroughME3Path;
                    }
                }
                else
                {
                    Log.Error("Could not parse command line arguments! Args: " + string.Join(' ', args));
                }
            }

            #endregion
        }

        #region Updates
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
        #endregion

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
            public string PassthroughME1Path { get; private set; }

            [Option("me2path",
                HelpText = "Sets the path for Mass Effect 2 on app boot. It must point to the game root directory.")]
            public string PassthroughME2Path { get; private set; }

            [Option("me3path",
                HelpText = "Sets the path for Mass Effect 3 on app boot. It must point to the game root directory.")]
            public string PassthroughME3Path { get; private set; }

            [Option("update-boot",
                HelpText = "Indicates that the process should run in update mode for a single file .net core executable. The process will exit upon starting because the platform extraction process will have completed.")]
            public bool UpdateBoot { get; private set; }

        }
    }
}
