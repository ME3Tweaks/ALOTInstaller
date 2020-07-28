using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Class for handling the Texture Library
    /// </summary>
    public static class TextureLibrary
    {
        private static FileSystemWatcher watcher;
        public static void SetupLibraryWatcher()
        {
            if (watcher != null)
            {
                watcher.Changed -= OnLibraryFileChanged;
                watcher.Created -= OnLibraryFileChanged;
                watcher.Deleted -= OnLibraryFileChanged;
                watcher.Renamed -= OnLibraryFileChanged;
                watcher.Dispose();
            }
            watcher = new FileSystemWatcher(Settings.TextureLibraryLocation)
            {
                NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.Size,

                Filter = "*.zip;*.tpf;*.mem;*.rar;*.7z"
            };

            // Add event handlers.
            watcher.Changed += OnLibraryFileChanged;
            watcher.Created += OnLibraryFileChanged;
            watcher.Deleted += OnLibraryFileChanged;
            watcher.Renamed += OnLibraryFileChanged;
        }

        private static void OnLibraryFileChanged(object sender, FileSystemEventArgs e)
        {
            Debug.WriteLine("Change " + e.ChangeType);
        }
    }
}
