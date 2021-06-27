using System;
using System.Diagnostics;
using System.IO;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using LegendaryExplorerCore.Packages;
using Serilog;

namespace ALOTInstallerCore.ModManager.Objects
{

    public class InstalledExtraFile
    {
        private MEGame game;
        public string DisplayName { get; }
        public string DllProductName { get; }
        public Version DllVersion { get; }
        public enum EFileType
        {
            DLL
        }

        public EFileType FileType { get; }
        public InstalledExtraFile(string filepath, EFileType type, MEGame game)
        {
            this.game = game;
            FilePath = filepath;
            FileName = Path.GetFileName(filepath);
            FileType = type;
            DisplayName = FileName;
            switch (type)
            {
                case EFileType.DLL:
                    var info = FileVersionInfo.GetVersionInfo(FilePath);
                    if (!string.IsNullOrWhiteSpace(info.ProductName))
                    {
                        DllProductName = info.ProductName.Trim();
                        DllVersion = info.ToVersion();
                        DisplayName += $@" ({info.ProductName.Trim()} {DllVersion})";
                    }
                    break;
                // Other versions if we ever need them
            }
        }

        private bool CanDeleteFile() => !Utilities.IsGameRunning(game);

        private void DeleteExtraFile()
        {
            if (!Utilities.IsGameRunning(game))
            {
                try
                {
                    File.Delete(FilePath);
                }
                catch (Exception e)
                {
                    Log.Error($@"[AICORE] Error deleting extra file {FilePath}: {e.Message}");
                }
            }
        }

        public string FileName { get; set; }

        public string FilePath { get; set; }
    }
}
