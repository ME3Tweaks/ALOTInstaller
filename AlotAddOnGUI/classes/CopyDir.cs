using ByteSizeLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static AlotAddOnGUI.MainWindow;

namespace AlotAddOnGUI.classes
{
    class CopyDir
    {
        public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target, BackgroundWorker worker = null)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (fi.FullName.EndsWith(".txt"))
                {
                    continue; //don't copy logs
                }
                //Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                try
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }
                catch (Exception e)
                {
                    Log.Error("Error copying file: " + fi + " -> " + Path.Combine(target.FullName, fi.Name));
                    throw e;
                }
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        public static int CopyAll_ProgressBar(DirectoryInfo source, DirectoryInfo target, BackgroundWorker worker, MainWindow mainWindow, int total, int done, string[] ignoredExtensions = null)
        {
            if (total == -1)
            {
                //calculate number of files
                total = Directory.GetFiles(source.FullName, "*.*", SearchOption.AllDirectories).Length;
                mainWindow.Progressbar_Max = total;
            }
            worker.ReportProgress(0, new ThreadCommand(UPDATE_PROGRESSBAR_INDETERMINATE, false));

            int numdone = done;
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                if (ignoredExtensions != null)
                {
                    bool skip = false;
                    foreach (string str in ignoredExtensions)
                    {
                        if (fi.Name.ToLower().EndsWith(str))
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip)
                    {
                        numdone++;
                        mainWindow.ProgressBarValue = numdone;
                        //worker.ReportProgress((int)((numdone * 1.0 / total) * 100.0));
                        continue;
                    }
                }
                string displayName = fi.Name;
                string path = Path.Combine(target.FullName, fi.Name);
                if (path.ToLower().EndsWith(".sfar") || path.ToLower().EndsWith(".tfc"))
                {
                    long length = new System.IO.FileInfo(fi.FullName).Length;
                    displayName += " (" + ByteSize.FromBytes(length) + ")";
                }
                worker.ReportProgress(done, new ThreadCommand(UPDATE_ADDONUI_CURRENTTASK, displayName));
                try
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }
                catch (Exception e)
                {
                    Log.Error("Error copying file: " + fi + " -> " + Path.Combine(target.FullName, fi.Name));
                    throw e;
                }
                // Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                numdone++;
                mainWindow.ProgressBarValue = numdone;
                //worker.ReportProgress((int)((numdone * 1.0 / total) * 100.0));
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                numdone = CopyAll_ProgressBar(diSourceSubDir, nextTargetSubDir, worker, mainWindow, total, numdone);
            }
            return numdone;
        }



        // Output will vary based on the contents of the source directory.
    }
}