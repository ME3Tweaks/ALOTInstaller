using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ALOTInstallerCore
{
    // LINUX SPECIFIC ITEMS IN UTILITIES CLASS
#if LINUX
    public static partial class Utilities
    {
        /// <summary>
        /// Chmod +x's the listed filepath, making it executable by the current user
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static int MakeFileExecutable(string filePath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "/bin/bash",
                Arguments = $"-c \" chmod +x  \"{filePath}\"\"",
                CreateNoWindow = true
            };

            Process proc = new Process() { StartInfo = startInfo, };
            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode;
        }
    }

#endif
}
