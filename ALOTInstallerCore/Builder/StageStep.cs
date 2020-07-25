using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using Serilog;

namespace ALOTInstallerCore.Builder
{
    // Extraction + Staging <<
    // Building
    // Installing


    /// <summary>
    /// Object that handles the staging step of texture package building
    /// </summary>
    public class StageStep
    {
        private List<InstallerFile> files;
        public StageStep(List<InstallerFile> filesToPrepare)
        {
            files = filesToPrepare;
        }

        private void ExtractFile(InstallerFile instFile, int buildID)
        {
            string filepath = null;
            if (instFile is ManifestFile mf)
            {
                filepath = Path.Combine(Locations.TextureLibraryLocation, mf.Filename);
            }

            var extension = Path.GetExtension(filepath);
            if (extension == ".mem") return; //no need to process this file.
            if (extension == ".tpf") return; //This file will be broken down at the next step
            if (extension == ".dds") return; //no need to extract this file
            if (extension == ".png") return; //no need to extract this file

            var outputPath = Path.Combine(Locations.BuildLocation, buildID.ToString());
            Directory.CreateDirectory(outputPath);
            object lockObject = new object();
            void appStart(int processID)
            {
                // This might need to be waited on after method is called.
                lock (lockObject)
                {
                    Monitor.Wait(lockObject);
                }
            }

            void handleIPC(string command, string param)
            {
                switch (command)
                {
                    case "TASK_PROGRESS":

                        break;
                    case "FILENAME":

                        break;
                }
            }

            void appExited(int code)
            {
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            }

            switch (extension)
            {
                case ".7z":
                case ".rar":
                case ".zip":
                    // Extract archive
                    MEMIPCHandler.RunMEMIPC($"--unpack-archive --input \"{filepath}\" --output \"{outputPath}\" --ipc",
                        appStart, 
                        handleIPC, 
                        x => Log.Error($"StdError on {filepath}: {x}"), 
                        appExited);

                    break;
            }
        }
    }
}
