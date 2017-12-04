using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }

        public static int CopyAll_ProgressBar(DirectoryInfo source, DirectoryInfo target, BackgroundWorker worker, int total, int done)
        {
            if (total == -1)
            {
                //calculate number of files
                total = Directory.GetFiles(source.FullName, "*.*", SearchOption.AllDirectories).Length;
            }

            int numdone = done;

            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                worker.ReportProgress(done, new ThreadCommand(UPDATE_OPERATION_LABEL, fi.Name));
                Log.Information(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                numdone++;
                worker.ReportProgress((int)((numdone * 1.0/ total) * 100.0));
                Debug.WriteLine(fi.Name +" "+numdone+" / "+total+" PERCENT: " + (int)((done * 1.0 / total) * 100.0));
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                numdone = CopyAll_ProgressBar(diSourceSubDir, nextTargetSubDir, worker, total, numdone);
            }
            return numdone;
        }

        // Output will vary based on the contents of the source directory.
    }
}