using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.ModManager.Objects;
using Serilog;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Describes a file in the manifest.
    /// </summary>
    public class ManifestFile : InstallerFile, INotifyPropertyChanged //this must be here to make fody run on this
    {

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="source"></param>
        public ManifestFile(ManifestFile source) : base(source)
        {
            RecommendationReason = source.RecommendationReason;
            Recommendation = source.Recommendation;
            UnpackedSingleFilename = source.UnpackedSingleFilename;
            TorrentFilename = source.TorrentFilename;
            ChoiceFiles = source.ChoiceFiles.Select(x => new ChoiceFile(x)).ToList();
            Recommendation = source.Recommendation;
            ComparisonsLink = source.ComparisonsLink;
            CopyFiles = source.CopyFiles.Select(x => new CopyFile(x)).ToList();
            DownloadLink = source.DownloadLink;
            FileMD5 = source.FileMD5;
            StageModFiles = source.StageModFiles;
            Tooltipname = source.Tooltipname;
            ZipFiles = source.ZipFiles.Select(x => new ZipFile(x)).ToList();
            RecommendationString = source.RecommendationString;
            ExtraInstructions = source.ExtraInstructions;
            OptionGroup = source.OptionGroup;
            //probably more
        }
        public ManifestFile() { }

        /// <summary>
        /// String describing the reason for the recommendation
        /// </summary>
        public string RecommendationReason { get; set; }

        /// <summary>
        /// Loading indicator that this is an ME3 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me3
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME3;
                else
                    ApplicableGames &= ~ApplicableGame.ME3;
            }
        }
        /// <summary>
        /// Loading indicator that this is an ME2 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me2
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME2;
                else
                    ApplicableGames &= ~ApplicableGame.ME2;
            }
        }
        /// <summary>
        /// Loading indicator that this is an ME1 file. On setting this, it will set the bit in ApplicableGames. Do not use this variable, use ApplicableGames instead.
        /// </summary>
        internal bool m_me1
        {
            set
            {
                if (value)
                    ApplicableGames |= ApplicableGame.ME1;
                else
                    ApplicableGames &= ~ApplicableGame.ME1;
            }
        }

        /// <summary>
        /// A mutual exclusive group option. If multiple files are ready at install time with same option group,
        /// a dialog must be shown so the user can pick which one to use. Doing more than one will waste disk space and time
        /// </summary>
        public string OptionGroup { get; set; }

        public string UnpackedSingleFilename { get; set; }

        public string TorrentFilename { get; set; }

        /// <summary>
        /// If this file is backed by the unpacked version. This call only works when file is in the library and not staged
        /// </summary>
        /// <returns></returns>
        public bool IsBackedByUnpacked()
        {
            if (UnpackedSingleFilename == null) return false;
            return Path.GetFileName(GetUsedFilepath()) == UnpackedSingleFilename;
        }
        /// <summary>
        /// Priority for sorting in the UI. Lower numbers are sorted higher.
        /// </summary>
        public int UIPriority { get; set; }

        public string Tooltipname { get; set; }

        public string DownloadLink { get; set; }
        public List<ChoiceFile> ChoiceFiles { get; set; } = new List<ChoiceFile>();
        public List<ZipFile> ZipFiles { get; set; } = new List<ZipFile>();
        public List<CopyFile> CopyFiles { get; set; } = new List<CopyFile>();
        public List<CompatibilityPrecheck> CompatibilityChecks { get; } = new List<CompatibilityPrecheck>();

        public string FileMD5 { get; internal set; }
        public string UnpackedFileMD5 { get; set; }
        public long UnpackedFileSize { get; set; }
        /// <summary>
        /// The recommendation type for this manifest file. This can be used to determine UI colors
        /// </summary>
        public RecommendationType Recommendation { get; set; }
        /// <summary>
        /// The recommendation string (from the manifest) for the file for this installer file. Ideally, it should match a recommendation type.
        /// </summary>
        public string RecommendationString { get; set; }

        public void OnRecommendationStringChanged()
        {
            if (RecommendationString == null)
            {
                Recommendation = RecommendationType.None;
                return;
            }

            if (Enum.TryParse<RecommendationType>(RecommendationString, out var r))
            {
                Recommendation = r;
            }
        }

        public override string ToString() => FriendlyName;

        public override string Category
        {
            get
            {
                if (AlotVersionInfo.IsNotVersioned)
                {
                    return "Addon";
                }

                if (AlotVersionInfo.MEUITMVER > 0)
                {
                    return "MEUITM";
                }

                return null;
            }
        }

        public override bool UpdateReadyStatus()
        {
            var updated = internalUpdateReadyStatus();
            if (updated)
            {
                Log.Information($"[AICORE] ManifestFile {FriendlyName} changing ready status. Is now ready: {Ready}");
            }

            return updated;
        }

        private bool internalUpdateReadyStatus()
        {
            var oldReady = Ready;
            var fp = GetUsedFilepath();
            if (File.Exists(fp))
            {
                var filesize = new FileInfo(fp).Length;
                if (Path.GetFileName(fp) == Filename || (TorrentFilename != null && Path.GetFileName(fp).Equals(TorrentFilename)))
                {
                    Ready = filesize == FileSize;
                    updateStatus();
                    return oldReady != Ready;
                }

                if (UnpackedSingleFilename != null && Path.GetFileName(fp).Equals(UnpackedSingleFilename))
                {
                    Ready = filesize == UnpackedFileSize;
                    updateStatus();
                    return oldReady != Ready;
                }
            }
            Ready = false;
            updateStatus();
            return oldReady != Ready;
        }

        /// <summary>
        /// Updates the ready status text
        /// </summary>
        private void updateStatus()
        {
            NotifyStatusUpdate();
            if (Disabled)
            {
                StatusText = "Disabled, will not install";
                return;
            }
            if (Ready)
            {
                StatusText = "Imported, ready to install";
                return;
            }
            StatusText = "Not imported, not ready to install";
        }

        /// <summary>
        /// Gets the backing file for this object. The unpacked single file version will be used if this file supports single unpacked files and the file is present.
        /// This method will check if unpacked file exists and is the correct size. It will not check if packed file exists or is the correct size.
        /// </summary>
        /// <returns></returns>
        public override string GetUsedFilepath()
        {
            if (UnpackedSingleFilename != null && UnpackedFileSize > 0 && UnpackedFileMD5 != null)
            {
                // This file supports unpacked mode
                var filePathUnpacked = Path.Combine(Settings.TextureLibraryLocation, UnpackedSingleFilename);
                if (File.Exists(filePathUnpacked) && new FileInfo(filePathUnpacked).Length == UnpackedFileSize)
                {
                    return filePathUnpacked;
                }
            }

            // Check if torrent file exists.
            if (TorrentFilename != null)
            {
                var filepathTorrent = Path.Combine(Settings.TextureLibraryLocation, TorrentFilename);
                if (File.Exists(filepathTorrent) && new FileInfo(filepathTorrent).Length == FileSize)
                {
                    return filepathTorrent;
                }
            }
            return Path.Combine(Settings.TextureLibraryLocation, Filename);
        }

        /// <summary>
        /// Path of the file that was used when staging began. This can be used to determine where to move a file back if this file was moved, or
        /// to find out if a packed or unpacked file was used
        /// </summary>
        public string StagedName { get; set; }

        /// <summary>
        /// Indicates that a mod file should be staged rather than decompiled if encountered when staging the manifest file
        /// </summary>
        public override bool StageModFiles { get; set; }
        /// <summary>
        /// Link to where one can view comparisons for this file, such as vanilla to this, or between versions if this mod supports ChoiceFiles.
        /// </summary>
        public string ComparisonsLink { get; set; }
        /// <summary>
        /// Mode this manifest file belongs in
        /// </summary>
        public ManifestMode Mode { get; set; }
        /// <summary>
        /// Extra instructions when downloading the file, e.g. to help clear up ambiguity
        /// </summary>
        public string ExtraInstructions { get; set; }

        /// <summary>
        /// Shim to pass an XElement for parsing through from Linq. Converts xelement into compatibility rules
        /// </summary>
        internal XElement CompatibilityPrechecksShim
        {
            set
            {
                if (value != null)
                {
                    // list of conditions we use to block install based on compatibility
                    CompatibilityChecks.AddRange(from compat in value.Descendants("dlcprecheck")
                        select new DLCCompatibilityPrecheck(compat));
                    CompatibilityChecks.AddRange(from compat in value.Descendants("fileprecheck")
                        select new FileCompatibilityPrecheck(compat));
                    CompatibilityChecks.AddRange(from compat in value.Descendants("packageprecheck")
                        select new PackageCompatibilityPrecheck(compat));
                }
            }
        }

        /// <summary>
        /// Gets the backing MD5 for this file
        /// </summary>
        /// <returns></returns>
        public string GetBackingHash()
        {
            if (IsBackedByUnpacked()) return UnpackedFileMD5;
            return FileMD5;
        }

        public void DisableIfIncompatible(GameTarget gameTarget)
        {
            foreach (var compat in CompatibilityChecks)
            {
                if (!compat.IsCompatibleConfig(gameTarget))
                {
                    ForceDisabled = true;
                    Disabled = true;
                    RecommendationReason = compat.IncompatibleMessage;
                    break;
                }
            }
        }
    }
}