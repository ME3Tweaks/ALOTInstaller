using System;
using System.Diagnostics;
using System.IO;
using ALOTInstallerCore.Objects;
using Serilog;

namespace ALOTInstallerCore.ModManager.Objects
{

    public class InstalledExtraFile
    {
        private Enums.MEGame game;
        public string DisplayName { get; }
        public enum EFileType
        {
            DLL
        }

        public EFileType FileType { get; }
        public InstalledExtraFile(string filepath, EFileType type, Enums.MEGame game)
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
                        DisplayName += $@" ({info.ProductName.Trim()})";
                    }
                    break;
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
                    Log.Error($@"Error deleting extra file {FilePath}: {e.Message}");
                }
            }
        }

        public string FileName { get; set; }

        public string FilePath { get; set; }
    }
}
