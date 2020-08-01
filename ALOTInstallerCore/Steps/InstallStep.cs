using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Steps.Installer;
using Serilog;

namespace ALOTInstallerCore.Steps
{
    /// <summary>
    /// Object that handles the install step of texture installation.
    /// </summary>
    public class InstallStep
    {
        private InstallOptionsPackage package;
        private ProgressHandler ProgressManager { get; }
        public Action<string> SetTopTextCallback { get; set; }
        public Action<string> SetMiddleTextCallback { get; set; }
        public Action<string> SetBottomTextCallback { get; set; }
        public Action<bool> SetTopTextVisibilityCallback { get; set; }
        public Action<bool> SetMiddleTextVisibilityCallback { get; set; }
        public Action<bool> SetBottomTextVisibilityCallback { get; set; }
        /// <summary>
        /// Callback for setting the 'overall' progress value, from 0 to 100. Can be used to display things like progressbars.
        /// </summary>
        public Action<int> SetOverallProgressCallback { get; set; }

        public InstallStep(InstallOptionsPackage package)
        {
            ProgressManager = new ProgressHandler();
            this.package = package;
        }

        public void InstallTextures(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            #region common callbacks and objects
            object lockObject = new object();
            void appStart(int processID)
            {
                // This might need to be waited on after method is called.
                Debug.WriteLine(@"Process launched. Process ID: " + processID);
            }
            void appExited(int code)
            {
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            }
            #endregion

            #region Check for existing markers
            {
                string args = $"--check-for-markers --gameid {package.InstallTarget.Game.ToGameNum()} --ipc";
                if (package.DebugLogging)
                {
                    args += " --debug-logs";
                }

                List<string> badFiles = new List<string>();

                void handleIPC(string command, string param)
                {
                    switch (command)
                    {
                        case "TASK_PROGRESS":
                            SetTopTextCallback?.Invoke($"Checking game files for existing texture markers {param}%");
                            break;
                        case "FILENAME":

                            break;
                        case "ERROR_FILEMARKER_FOUND":
                            Log.Error("File was part of a different texture installation: " + param);
                            badFiles.Add(param);
                            break;
                        default:
                            Debug.WriteLine($"Unhandled IPC: {command} {param}");
                            break;
                    }
                }

                MEMIPCHandler.RunMEMIPC(args,
                    appStart,
                    handleIPC,
                    x => Log.Error($"StdError: {x}"),
                    appExited);
                lock (lockObject)
                {
                    Monitor.Wait(lockObject);
                }

                if (badFiles.Any())
                {
                    // Must abort!
                    SetBottomTextCallback?.Invoke("Couldn't install textures");
                    SetTopTextCallback?.Invoke("Files from a previous texture installation were found");
                    //SetContinueButtonVisibility?.Invoke(true);
                }
            }
            #endregion

            // Main installer
            Stage currentStage = null;

        }

    }
}
