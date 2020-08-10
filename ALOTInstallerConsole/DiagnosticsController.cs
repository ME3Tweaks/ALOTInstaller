using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ALOTInstallerConsole.UserControls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using NStack;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    public class DiagnosticsController
    {
        public static void InitDiagnostics()
        {
            var logFiles = new DirectoryInfo(LogCollector.LogDir)
                .GetFiles(@"*.txt")
                .OrderByDescending(f => f.LastWriteTime)
                .Select(x => x.FullName);

            string logfile = null;
            bool cont = false;
            ListChooserDialog lcd = null;
            Button continueButton = new Button("Continue")
            {
                Clicked = () =>
                {
                    logfile = lcd.SelectedItem;
                    cont = true;
                    Application.RequestStop(); //Close dialog
                }
            };
            Button abortButton = new Button("Abort upload")
            {
                Clicked = () =>
                {
                    cont = false;
                    Application.RequestStop(); //Close dialog
                }
            };

            lcd = new ListChooserDialog("Select log file", "Select which log file to upload", "", logFiles.Select(x => Path.GetFileName(x)).ToList(), continueButton);
            Application.Run(lcd);

            if (!cont)
            {
                return;//abort
            }

            List<string> paths = new List<string>();
            if (Locations.ME1Target != null) paths.Add("ME1");
            if (Locations.ME2Target != null) paths.Add("ME2");
            if (Locations.ME3Target != null) paths.Add("ME3");
            paths.Add("No Diag");
            paths.Add("Abort");
            var selectedIndex = MessageBox.Query("Select game", "Select which game to perform diagnostic on.", paths.Select(x => (ustring)x.ToString()).ToArray());
            if (paths[selectedIndex] == "Abort" || selectedIndex < 0) return;

            GameTarget target = null;
            if (paths[selectedIndex] == "ME1") target = Locations.ME1Target;
            if (paths[selectedIndex] == "ME2") target = Locations.ME2Target;
            if (paths[selectedIndex] == "ME3") target = Locations.ME3Target;

            bool texturesCheck = false;
            if (target != null)
            {
                selectedIndex = MessageBox.Query("Select diagnostic type",
                    "Select diagnostic type. Full will scan all textures and may take a few minutes.", "Full", "Quick",
                    "Abort");
                if (selectedIndex == 2)
                    return; //abort

                texturesCheck = selectedIndex == 0;
            }

            NamedBackgroundWorker nbw = new NamedBackgroundWorker("DiagnosticsWorker");
            ProgressDialog pd = new ProgressDialog("Uploading logs", "Please wait while logs are collected.")
            {
                ProgressMax = 100
            };
            nbw.DoWork += (a, b) =>
            {
                StringBuilder logUploadText = new StringBuilder();

                string logText = "";
                if (target != null)
                {
                    logUploadText.Append("[MODE]diagnostics\n"); //do not localize
                    logUploadText.Append(LogCollector.PerformDiagnostic(target, texturesCheck,
                        x => Application.MainLoop.Invoke(() => pd.BottomMessage = x),
                        x => Application.MainLoop.Invoke(() => pd.ProgressValue = x)
                    ));
                    logUploadText.Append("\n"); //do not localize
                }

                if (logfile != null)
                {
                    logUploadText.Append("[MODE]logs\n"); //do not localize
                    logUploadText.AppendLine(LogCollector.CollectLogs(Path.Combine(LogCollector.LogDir, logfile)));
                    logUploadText.Append("\n"); //do not localize
                }

                b.Result = logUploadText.ToString();
            };
            nbw.RunWorkerCompleted += (abortButton, b) =>
            {
                pd.BottomMessage = "Uploading log";
                var response = LogUploader.UploadLog(b.Result as string, "https://me3tweaks.com/alot/logupload3");
                if (pd.IsCurrentTop)
                {
                    Application.RequestStop(); //Close dialog
                }

                if (response.StartsWith("http"))
                {
                    Utilities.OpenWebPage(response);
                }
                else
                {
                    MessageBox.Query("Error uploading to server", response, "OK");
                }
            };
            nbw.RunWorkerAsync();
            Application.Run(pd);



        }

        /// <summary>
        /// Handles diagnostics information
        /// </summary>
        class ListChooserDialog : Dialog
        {
            private List<string> options;
            private ListView lv;
            public string SelectedItem => options[lv.SelectedItem];
            public ListChooserDialog(string title, string aboveComboMessage, string belowComboMessage, List<string> options, params Button[] buttons) : base(title, buttons)
            {
                this.options = options;
                Width = 50;
                Height = 20;
                int y = 0;
                Add(new Label(aboveComboMessage)
                {
                    X = 0,
                    Y = y++,
                    Height = 2,
                    Width = Dim.Fill(),
                });
                y++;
                lv = new ListView(options)
                {
                    X = 0,
                    Y = y++,
                    Height = 10,
                    Width = Dim.Fill(),
                };
                Add(lv);
                Add(new Label(belowComboMessage)
                {
                    X = 0,
                    Y = y++,
                    Height = 1,
                    Width = Dim.Fill(),
                });
            }
        }
    }
}