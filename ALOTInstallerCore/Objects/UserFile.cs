using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using ALOTInstallerCore.Objects.Manifest;

namespace ALOTInstallerCore.Objects
{
    /// <summary>
    /// A user file is a file that is not listed in the manifest and is added to the installer by the user
    /// </summary>
    public class UserFile : InstallerFile, INotifyPropertyChanged //requires INotifyPropertyChanged so Fody injects into it
    {

        public UserFile()
        {
            Author = "User files";
        }
        /// <summary>
        /// The full path to the user file
        /// </summary>
        public string FullFilePath { get; set; }

        /// <summary>
        /// User Files will always indicate that mod files should be staged rather than decompiled as users will have no idea what it means to decompile the mod file
        /// </summary>
        public override bool StageModFiles
        {
            get => true;
            set { } //Must have setter even if unused
        }


        public override string Category => "User file";
        /// <summary>
        /// UI displayable string of this userfile (if any)
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Updates the ready status for this user file. Checks if file exists on disk and sets Ready based on this returned value
        /// </summary>
        /// <returns></returns>
        public override bool UpdateReadyStatus()
        {
            var oldReady = Ready;
            var fp = GetUsedFilepath();
            Ready = File.Exists(fp);
            Debug.WriteLine($"USER {FriendlyName} ready: {Ready}");
            var readyStateChanged = oldReady != Ready;
            if (readyStateChanged)
            {
                if (!Ready) Description = "File no longer available";
                NotifyStatusUpdate();
            }
            return readyStateChanged;
        }

        /// <summary>
        /// Gets the backing file for this UserFile
        /// </summary>
        /// <returns></returns>
        public override string GetUsedFilepath() => FullFilePath;
    }
}
