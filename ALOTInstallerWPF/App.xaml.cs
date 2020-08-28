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
                        copyAndRebootUpdate(parsedCommandLineArgs.Value.UpdateRebootDest);
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

        private void copyAndRebootUpdate(string valueUpdateRebootDest)
        {
            Thread.Sleep(2000); //SLEEP WHILE WE WAIT FOR PARENT PROCESS TO STOP.
            Log.Information("In update mode. Update destination: " + valueUpdateRebootDest);
            int i = 0;
            while (i < 5)
            {

                i++;
                try
                {
                    Log.Information("Applying update");
                    if (File.Exists(valueUpdateRebootDest)) File.Delete(valueUpdateRebootDest);
                    File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, valueUpdateRebootDest);
                    ProcessStartInfo psi = new ProcessStartInfo(valueUpdateRebootDest)
                    {
                        WorkingDirectory = Directory.GetParent(valueUpdateRebootDest).FullName
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
                        MessageBox.Show("Update was unable to apply. See the logs directory for more information. If this continues to happen please come to the ALOT discord or download a new copy from GitHub.");
                        Environment.Exit(1);
                    }
                }
            }
        }

        class Options
        {
            [Option("update-dest-path",
                HelpText = "Copies this program's executable to the specified location, runs the new executable, and then exits this process.")]
            public string UpdateRebootDest { get; private set; }

            [Option("update-boot",
                HelpText = "Indicates that the process should run in update mode for a single file .net core executable. The process will exit upon starting because the platform extraction process will have completed.")]
            public bool UpdateBoot { get; private set; }

        }
    }
}
