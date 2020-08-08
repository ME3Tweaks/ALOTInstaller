using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// A user file is a file that is not listed in the manifest and is added to the installer by the user.
    /// </summary>
    public class UserFile : InstallerFile
    {
        public string FullFilePath { get; set; }

        /// <summary>
        /// Updates the ready status for this user file. Checks if file exists on disk and sets Ready based on this returned value
        /// </summary>
        /// <returns></returns>
        public override bool UpdateReadyStatus()
        {
            var oldReady = Ready;
            var fp = GetUsedFilepath();
            Ready = File.Exists(fp);
            return oldReady != Ready;
        }

        /// <summary>
        /// Gets the backing file for this UserFile.
        /// </summary>
        /// <returns></returns>
        public override string GetUsedFilepath() => FullFilePath;
    }
}
