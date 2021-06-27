using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.asi;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Helpers;
using MassEffectModManagerCore.modmanager.asi;
using Serilog;
using Path = System.IO.Path;

namespace ALOTInstallerCore.ModManager.Objects
{
    public class GameTarget : IEqualityComparer<GameTarget>, INotifyPropertyChanged
    {
        public const uint MEMI_TAG = 0x494D454D;

        public event PropertyChangedEventHandler PropertyChanged;

        public MEGame Game { get; }
        public string TargetPath { get; }
        public bool RegistryActive { get; set; }
        public string GameSource { get; private set; }
        public string ExecutableHash { get; private set; }

        public string TargetBootIcon
        {
            get
            {
                if (GameSource == null) return @"/images/unknown.png";
                if (GameSource.Contains(@"Steam")) return @"/images/steam.png"; //higher priority than Origin in icon will make the Steam/Origin mix work
                if (GameSource.Contains(@"Origin")) return @"/images/origin.png";
                if (GameSource.Contains(@"DVD")) return @"/images/dvd.png";
                return @"/images/unknown.png";
            }
        }

        public bool Supported => !string.IsNullOrEmpty(GameSource);
        public bool IsPolishME1 { get; private set; }

        /// <summary>
        /// Determines if this gametarget can be chosen in dropdowns
        /// </summary>
        public bool Selectable { get; internal set; } = true;

        public string ALOTVersion { get; private set; }

        /// <summary>
        /// Indicates that this is a custom, abnormal game object. It may be used only for UI purposes, but it depends on the context.
        /// </summary>
        public bool IsCustomOption { get; set; } = false;

        public GameTarget(MEGame game, string targetRootPath, bool currentRegistryActive, bool isCustomOption = false)
        {
            this.Game = game;
            this.RegistryActive = currentRegistryActive;
            this.IsCustomOption = isCustomOption;
            this.TargetPath = targetRootPath.TrimEnd('\\', '/');
            ReloadGameTarget(true, false);
        }

        public void ReloadGameTarget(bool logSource = true, bool forceLodUpdate = false)
        {
            if (Game != MEGame.Unknown && !IsCustomOption)
            {
                if (Directory.Exists(TargetPath))
                {
                    var alotInfo = GetInstalledALOTInfo();
                    if (alotInfo != null)
                    {
                        TextureModded = true;
                        ALOTVersion = alotInfo.ToString();
                        if (alotInfo.MEUITMVER > 0)
                        {
                            MEUITMInstalled = true;
                            MEUITMVersion = alotInfo.MEUITMVER;
                        }
                    }
                    else
                    {
                        TextureModded = false;
                        ALOTVersion = null;
                        MEUITMInstalled = false;
                        MEUITMVersion = 0;
                    }

                    CLog.Information(@"[AICORE] Getting game source for target " + TargetPath, logSource);
                    var hashCheckResult = VanillaDatabaseService.GetGameSource(this);

                    GameSource = hashCheckResult.result;
                    ExecutableHash = hashCheckResult.hash;
                    if (GameSource == null)
                    {
                        CLog.Error(@"[AICORE] Unknown source or illegitimate installation: " + hashCheckResult.hash, logSource);

                    }
                    else
                    {
                        if (GameSource.Contains(@"Origin") && Game == MEGame.ME3)
                        {
                            // Check for steam
                            if (Directory.Exists(Path.Combine(TargetPath, @"__overlay")))
                            {
                                GameSource += @" (Steam version)";
                            }
                        }

                        CLog.Information(@"[AICORE] Source: " + GameSource, logSource);
                    }

                    IsPolishME1 = Game == MEGame.ME1 && File.Exists(Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"niebieska_pl.bik"));
                    if (IsPolishME1)
                    {
                        CLog.Information(@"[AICORE] ME1 Polish Edition detected", logSource);
                    }

                    if (forceLodUpdate)
                    {
                        UpdateLODs();
                    }
                }
                else
                {
                    Log.Error($@"[AICORE] Target is invalid: {TargetPath} does not exist (or is not accessible)");
                    IsValid = false; //set to false if target becomes invalid
                }
            }
        }

        public void UpdateLODs(bool twoK = false)
        {
            var textureInfo = GetInstalledALOTInfo();
            LodSetting setting = LodSetting.Vanilla;
            if (textureInfo != null)
            {
                setting |= twoK ? LodSetting.TwoK : LodSetting.FourK;
                if (Game == MEGame.ME1)
                {
                    if (textureInfo.MEUITMVER > 0)
                    {
                        //detect soft shadows/meuitm
                        var branchingPCFCommon = Path.Combine(TargetPath, @"Engine", @"Shaders", @"BranchingPCFCommon.usf");
                        if (File.Exists(branchingPCFCommon))
                        {
                            if (Utilities.CalculateMD5(branchingPCFCommon) == @"10db76cb98c21d3e90d4f0ffed55d424")
                            {
                                setting |= LodSetting.SoftShadows;
                            }
                        }
                    }
                }
            }

            MEMIPCHandler.SetLODs(Game, setting);
        }

        public bool Equals(GameTarget x, GameTarget y)
        {
            return x.TargetPath == y.TargetPath && x.Game == y.Game;
        }

        public int GetHashCode(GameTarget obj)
        {
            return obj.TargetPath.GetHashCode();
        }

        public bool TextureModded { get; private set; }

        public List<TextureModInstallationInfo> GetTextureModInstallationHistory()
        {
            var alotInfos = new List<TextureModInstallationInfo>();
            int startPos = -1;
            while (GetInstalledALOTInfo(startPos) != null)
            {
                var info = GetInstalledALOTInfo(startPos);
                alotInfos.Add(info);
                startPos = info.MarkerStartPosition;
            }

            return alotInfos;
        }

        /// <summary>
        /// Gets the installed texture mod info. If startpos is not defined (<0) the latest version is used from the end of the file.
        /// </summary>
        /// <param name="startpos"></param>
        /// <returns></returns>
        public TextureModInstallationInfo GetInstalledALOTInfo(int startPos = -1)
        {
            string gamePath = getALOTMarkerFilePath();
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read);
                    if (startPos < 0)
                    {
                        fs.SeekEnd();
                    }
                    else
                    {
                        fs.Seek(startPos, SeekOrigin.Begin);
                    }

                    long endPos = fs.Position;
                    fs.Position = endPos - 4;
                    uint memi = fs.ReadUInt32();

                    if (memi == MEMI_TAG)
                    {
                        long markerStartOffset = fs.Position;
                        //MEM has been run on this installation
                        fs.Position = endPos - 8;
                        short installerVersionUsed = fs.ReadInt16();
                        short memVersionUsed = fs.ReadInt16();
                        fs.Position -= 4; //roll back so we can read this whole thing as 4 bytes
                        int preMemi4Bytes = fs.ReadInt32();
                        int perGameFinal4Bytes = -20;
                        switch (Game)
                        {
                            case MEGame.ME1:
                                perGameFinal4Bytes = 0;
                                break;
                            case MEGame.ME2:
                                perGameFinal4Bytes = 4352;
                                break;
                            case MEGame.ME3:
                                perGameFinal4Bytes = 16777472;
                                break;
                        }

                        // Note: If MEMI v1 is written after any other MEMI marker, it will not work as we cannot differentiate v1 to v2+

                        if (preMemi4Bytes != perGameFinal4Bytes) //default bytes before 178 MEMI Format (MEMI v1)
                        {
                            // MEMI v3 (and technically also v2 but values will be wrong)
                            fs.Position = endPos - 12;
                            short ALOTVER = fs.ReadInt16();
                            byte ALOTUPDATEVER = (byte)fs.ReadByte();
                            byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                            //unused for now
                            fs.Position = endPos - 16;

                            markerStartOffset = fs.Position;
                            int MEUITMVER = fs.ReadInt32();

                            var tmii = new TextureModInstallationInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER, memVersionUsed, installerVersionUsed);
                            tmii.MarkerExtendedVersion = 0x03; // detected memi v3
                            tmii.MarkerStartPosition = (int)markerStartOffset;

                            // MEMI v4 DETECTION
                            fs.Position = endPos - 20;
                            if (fs.ReadUInt32() == TextureModInstallationInfo.TEXTURE_MOD_MARKER_VERSIONING_MAGIC)
                            {
                                // It's MEMI v4 (or higher)
                                var memiExtendedEndPos = endPos - 24; // Sanity check should make reading end here
                                fs.Position = memiExtendedEndPos;
                                fs.Position = fs.ReadInt32(); // Go to start of MEMI extended marker
                                tmii.MarkerStartPosition = (int)fs.Position;
                                tmii.MarkerExtendedVersion = fs.ReadInt32();
                                // Extensions to memi format go here

                                if (tmii.MarkerExtendedVersion == 0x04)
                                {
                                    tmii.InstallerVersionFullName = fs.ReadUnrealString();
                                    tmii.InstallationTimestamp = DateTime.FromBinary(fs.ReadInt64());
                                    var fileCount = fs.ReadInt32();
                                    for (int i = 0; i < fileCount; i++)
                                    {
                                        tmii.InstalledTextureMods.Add(new TextureModInstallationInfo.InstalledTextureMod(fs, tmii.MarkerExtendedVersion));
                                    }
                                }

                                if (fs.Position != memiExtendedEndPos)
                                {
                                    Log.Warning($@"Sanity check for MEMI extended marker failed. We did not read data until the marker info offset. Should be at 0x{memiExtendedEndPos:X6}, but ended at 0x{fs.Position:X6}");
                                }
                            }

                            return tmii;
                        }

                        return new TextureModInstallationInfo(0, 0, 0, 0)
                        {
                            MarkerStartPosition = (int)markerStartOffset,
                            MarkerExtendedVersion = 0x01
                        }; //MEMI tag but no info we know of
                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"Error reading texture mod marker file for {Game}. Installed info will be returned as null (nothing installed). " + e.Message);
                    return null;
                }
            }

            return null;
            //Debug. Force ALOT always on
            //return new ALOTVersionInfo(9, 0, 0, 0); //MEMI tag but no info we know of
        }

        private string getALOTMarkerFilePath()
        {
            // this used to be shared method
            return M3Directories.GetTextureMarkerPath(this);
        }

        public ObservableCollectionExtended<ModifiedFileObject> ModifiedBasegameFiles { get; } = new ObservableCollectionExtended<ModifiedFileObject>();
        public ObservableCollectionExtended<SFARObject> ModifiedSFARFiles { get; } = new ObservableCollectionExtended<SFARObject>();

        public void PopulateModifiedBasegameFiles(Func<string, bool> restoreBasegamefileConfirmationCallback,
            Func<string, bool> restoreSfarConfirmationCallback,
            Action notifySFARRestoringCallback,
            Action notifyFileRestoringCallback,
            Action<object> notifyRestoredCallback)
        {
            ModifiedBasegameFiles.ClearEx();
            ModifiedSFARFiles.ClearEx();

            List<string> modifiedSfars = new List<string>();
            List<string> modifiedFiles = new List<string>();

            void failedCallback(string file)
            {
                if (file.EndsWith(@".sfar"))
                {
                    modifiedSfars.Add(file);
                    return;
                }

                if (file == getALOTMarkerFilePath())
                {
                    return; //Do not report this file as modified or user will desync game state with texture state
                }

                modifiedFiles.Add(file);
            }

            VanillaDatabaseService.ValidateTargetAgainstVanilla(this, failedCallback);

            ModifiedSFARFiles.AddRange(modifiedSfars.Select(file => new SFARObject(file, this)));
            ModifiedBasegameFiles.AddRange(modifiedFiles.Select(x => new ModifiedFileObject(x)));
        }

        public ObservableCollectionExtended<InstalledDLCMod> UIInstalledDLCMods { get; } = new ObservableCollectionExtended<InstalledDLCMod>();

        public void PopulateDLCMods(bool includeDisabled, Func<InstalledDLCMod, bool> deleteConfirmationCallback = null, Action notifyDeleted = null, bool modNamePrefersTPMI = false)
        {
            var dlcDir = M3Directories.GetDLCPath(this);
            var installedMods = M3Directories.GetInstalledDLC(this, includeDisabled).Where(x => !MEDirectories.OfficialDLC(Game).Contains(x.TrimStart('x'), StringComparer.InvariantCultureIgnoreCase));
            //Must run on UI thread
            //#if WPF
            //Application.Current.Dispatcher.Invoke(delegate
            //{
            //    UIInstalledDLCMods.ClearEx();
            //    UIInstalledDLCMods.AddRange(installedMods.Select(x => new InstalledDLCMod(Path.Combine(dlcDir, x), Game, deleteConfirmationCallback, notifyDeleted, modNamePrefersTPMI)).ToList().OrderBy(x => x.ModName));
            //});
            //#else
            UIInstalledDLCMods.ReplaceAll(installedMods.Select(x => new InstalledDLCMod(Path.Combine(dlcDir, x), Game, modNamePrefersTPMI)).ToList().OrderBy(x => x.ModName));
            //#endif
        }

        public bool IsTargetWritable()
        {
            return Utilities.IsDirectoryWritable(TargetPath) && Utilities.IsDirectoryWritable(Path.Combine(TargetPath, @"Binaries"));
        }

        public bool IsValid { get; set; }

        /// <summary>
        /// Validates a game directory by checking for multiple things that should be present in a working game.
        /// </summary>
        /// <param name="ignoreCmmVanilla">Ignore the check for a cmm_vanilla file. Presence of this file will cause validation to fail</param>
        /// <returns>String of failure reason, null if OK</returns>
        public string ValidateTarget(bool ignoreCmmVanilla = false)
        {
            if (!Selectable)
            {
                return null;
            }

            IsValid = false; //set to invalid at first/s
            string[] validationFiles = null;
            switch (Game)
            {
                case MEGame.ME1:
                    validationFiles = new[]
                    {
                            Path.Combine(TargetPath, @"Binaries", @"MassEffect.exe"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Maps", @"EntryMenu.SFM"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"BIOC_Base.u"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Packages", @"Textures", @"BIOA_GLO_00_A_Opening_FlyBy_T.upk"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Maps", @"WAR", @"LAY", @"BIOA_WAR20_05_LAY.SFM"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"MEvisionSEQ3.bik")
                        };
                    break;
                case MEGame.ME2:
                    validationFiles = new[]
                    {
                            Path.Combine(TargetPath, @"Binaries", @"MassEffect2.exe"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"BioA_BchLmL.pcc"),
                            Path.Combine(TargetPath, @"BioGame", @"Config", @"PC", @"Cooked", @"Coalesced.ini"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Wwise_Jack_Loy_Music.afc"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"WwiseAudio.pcc"),
                            Path.Combine(TargetPath, @"BioGame", @"Movies", @"Crit03_CollectArrive_Part2_1.bik")
                        };
                    break;
                case MEGame.ME3:
                    validationFiles = new[]
                    {
                            Path.Combine(TargetPath, @"Binaries", @"Win32", @"MassEffect3.exe"),
                            Path.Combine(TargetPath, @"BIOGame", @"CookedPCConsole", @"Textures.tfc"),
                            Path.Combine(TargetPath, @"BIOGame", @"CookedPCConsole", @"Startup.pcc"),
                            Path.Combine(TargetPath, @"BIOGame", @"CookedPCConsole", @"Coalesced.bin"),
                            Path.Combine(TargetPath, @"BIOGame", @"Patches", @"PCConsole", @"Patch_001.sfar"),
                            Path.Combine(TargetPath, @"BIOGame", @"CookedPCConsole", @"Textures.tfc"),
                            Path.Combine(TargetPath, @"BIOGame", @"CookedPCConsole", @"citwrd_rp1_bailey_m_D_Int.afc")
                        };
                    break;
            }

            if (validationFiles == null) return null; //Invalid game.
            foreach (var f in validationFiles)
            {
                if (!File.Exists(f))
                {
                    return "Invalid target: Missing file " + Path.GetFileName(f);
                }
            }

            // Check exe on first file
#if WINDOWS
            var exeInfo = FileVersionInfo.GetVersionInfo(validationFiles[0]);
            switch (Game)
            {
                /*
                MassEffect.exe 1.2.20608.0
                MassEffect2.exe 1.2.1604.0 (File Version)
                ME2Game.exe is the same
                MassEffect3.exe 1.5.5427.124
                */
                case MEGame.ME1:
                    if (exeInfo.FileVersion != @"1.2.20608.0")
                    {
                        // NOT SUPPORTED
                        return $"Unsupported executable version: {exeInfo.FileVersion}. The only supported version of MassEffect.exe is 1.2.20608.0";
                    }
                    break;
                case MEGame.ME2:
                    if (exeInfo.FileVersion != @"1.2.1604.0" && exeInfo.FileVersion != @"01604.00") // Steam and Origin exes have different FileVersion for some reason
                    {
                        // NOT SUPPORTED
                        return $"Unsupported executable version: {exeInfo.FileVersion}. The only supported version of MassEffect2.exe is 1.2.1604.0";
                    }
                    break;
                case MEGame.ME3:
                    if (exeInfo.FileVersion != @"05427.124") // not really sure what's going on here
                    {
                        // NOT SUPPORTED
                        return $"Unsupported executable version: {exeInfo.FileVersion}. The only supported version of MassEffect3.exe is 1.5.5427.124";
                    }
                    break;
            }
#endif

            if (!ignoreCmmVanilla)
            {
                if (File.Exists(Path.Combine(TargetPath, @"cmm_vanilla"))) return "Invalid target: This location was marked as a backup (cmm_vanilla file is present)";
            }

            IsValid = true;
            return null;
        }

        protected bool Equals(GameTarget other)
        {
            return TargetPath == other.TargetPath;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((GameTarget)obj);
        }

        public bool MEUITMInstalled { get; private set; }
        public int MEUITMVersion { get; private set; }

        public override int GetHashCode()
        {
            return (TargetPath != null ? TargetPath.GetHashCode() : 0);
        }

        private Queue<SFARObject> SFARRestoreQueue = new Queue<SFARObject>();

        public class SFARObject : INotifyPropertyChanged
        {
            public SFARObject(string file, GameTarget target)
            {
                IsModified = true;
                Unpacked = new FileInfo(file).Length == 32;
                DLCDirectory = Directory.GetParent(Directory.GetParent(file).FullName).FullName;
                FilePath = file.Substring(target.TargetPath.Length + 1);
                if (Path.GetFileName(file) == @"Patch_001.sfar")
                {
                    UIString = @"TestPatch";
                    IsMPSFAR = true;
                    IsSPSFAR = true;
                }
                else
                {
                    var dlcFoldername = Directory.GetParent(Directory.GetParent(file).FullName).FullName;
                    if (dlcFoldername.Contains(@"DLC_UPD") || dlcFoldername.Contains(@"DLC_CON_MP"))
                    {
                        IsMPSFAR = true;
                    }
                    else
                    {
                        IsSPSFAR = true;
                    }

                    ME3Directory.OfficialDLCNames.TryGetValue(Path.GetFileName(dlcFoldername), out var name);
                    UIString = name;
                    if (Unpacked)
                    {
                        UIString += @" - Unpacked";
                    }

                    var unpackedFiles = Directory.GetFiles(DLCDirectory, @"*", SearchOption.AllDirectories);
                    // not TOC is due to bug in autotoc
                    //TODO: UNIFY WITH UNPACKED METHODS
                    if (unpackedFiles.Any(x =>
                        Path.GetExtension(x) == @".bin" &&
                        Path.GetFileNameWithoutExtension(x) != @"PCConsoleTOC") && !Unpacked) Inconsistent = true;
                }
            }

            public bool IsSPSFAR { get; private set; }
            public bool IsMPSFAR { get; private set; }

            public bool IsModified { get; set; }

            public static bool HasUnpackedFiles(string sfarFile)
            {
                // TODO: UNIFY THE UNPACKED DETECTION METHODS
                var unpackedFiles =
                    Directory.GetFiles(Directory.GetParent(Directory.GetParent(sfarFile).FullName).FullName, @"*",
                        SearchOption.AllDirectories);
                return (unpackedFiles.Any(x =>
                    Path.GetExtension(x) == @".bin" && Path.GetFileNameWithoutExtension(x) != @"PCConsoleTOC"));
            }

            private readonly bool Unpacked;

            public string DLCDirectory { get; }

            public event PropertyChangedEventHandler PropertyChanged;

            public string FilePath { get; }
            public string UIString { get; }
            public bool Inconsistent { get; }

        }

        public class ModifiedFileObject : INotifyPropertyChanged
        {
            public string FilePath { get; }

            public event PropertyChangedEventHandler PropertyChanged;

            public ModifiedFileObject(string filePath)
            {
                this.FilePath = filePath;
            }
        }

        public void StampTextureModificationInfo(TextureModInstallationInfo tmii)
        {

            var markerPath = getALOTMarkerFilePath();
            try
            {
                using (FileStream fs = new FileStream(markerPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    // MARKER FILE FORMAT
                    // When writing marker, the end of the file is appended with the following data.
                    // Programs that read this marker must read the file IN REVERSE as the MEMI marker
                    // file is appended to prevent data corruption of an existing game file

                    // MEMI v1 - ALOT support
                    // This version only indicated that a texture mod (alot) had been installed
                    // BYTE "MEMI" ASCII

                    // MEMI v2 - MEUITM support (2018):
                    // This version supported versioning of main ALOT and MEUITM. On ME2/3, the MEUITM field would be 0
                    // INT MEUITM VERSION
                    // INT ALOT VERSION (major only)
                    // SHORT MEM VERSION USED
                    // SHORT INSTALLER VERSION USED
                    // <MEMI v1>

                    // MEMI v3 - ALOT subversioning support (2018):
                    // This version split the ALOT int into a short and 2 bytes. The size did not change.
                    // As a result it is not possible to distinguish v2 and v3, and code should just assume v3.
                    // INT MEUITM Version
                    // SHORT ALOT Version
                    // BYTE ALOT Update Version
                    // BYTE ALOT Hotfix Version (not used)
                    // SHORT MEM VERSION USED
                    // SHORT INSTALLER VERSION USED
                    // <MEMI v1>

                    // MEMI v4 - Extended (2020+):
                    // INT MEMI EXTENDED VERSION                         <---------------------------------------------
                    // UNREALSTRING Installer Version Info Extended                                                   |
                    // LONG BINARY DATESTAMP OF STAMPING TIME                                                         |
                    // INT INSTALLED FILE COUNT - ONLY COUNTS TEXTURE MODS, PREINSTALL MODS ARE NOT COUNTED           |
                    // FOR <INSTALLED FILE COUNT>                                                                     |
                    //     BYTE INSTALLED FILE TYPE                                                                   |
                    //         0 = USER FILE                                                                          |
                    //         1 = MANIFEST FILE                                                                      |
                    //     UNREALSTRING Installed File Name (INT LEN (negative for unicode), STR DATA)                |
                    //     [IF MANIFESTFILE] UNREALSTRING Author Name                                                 |
                    // INT MEMI Extended Marker Data Start Offset -----------------------------------------------------
                    // INT MEMI Extended Magic (0xDEADBEEF)
                    // <MEMI v3>

                    fs.SeekEnd();

                    // Write MEMI v4 - Installer full name, date, List of installed files
                    var memiExtensionStartPos = fs.Position;
                    fs.WriteInt32(TextureModInstallationInfo.LATEST_TEXTURE_MOD_MARKER_VERSION); // THIS MUST BE INCREMENTED EVERY TIME MARKER FORMAT CHANGES!! OR IT WILL BREAK OTHER APPS
                    fs.WriteUnrealStringUnicode($"{Utilities.GetAppPrefixedName()} Installer {Utilities.GetAppVersion()}");
                    fs.WriteInt64(DateTime.Now.ToBinary()); // DATESTAMP
                    fs.WriteInt32(tmii.InstalledTextureMods.Count); // NUMBER OF FILE ENTRIES TO FOLLOW. Count must be here
                    foreach (var fi in tmii.InstalledTextureMods)
                    {
                        fi.WriteToMarker(fs);
                    }
                    fs.WriteInt32((int)memiExtensionStartPos); // Start of memi extended data
                    fs.WriteUInt32(TextureModInstallationInfo.TEXTURE_MOD_MARKER_VERSIONING_MAGIC); // Magic that can be used to tell if this has the v3 extended marker offset preceding it

                    // Write MEMI v3
                    fs.WriteInt32(tmii.MEUITMVER); //meuitm
                    fs.WriteInt16(tmii.ALOTVER); //major
                    fs.WriteByte(tmii.ALOTUPDATEVER); //minor
                    fs.WriteByte(tmii.ALOTHOTFIXVER); //hotfix

                    // MEMI v2 is not used

                    // Write MEMI v1
                    fs.WriteInt16(tmii.ALOT_INSTALLER_VERSION_USED); //Installer Version (Build)
                    fs.WriteInt16(tmii.MEM_VERSION_USED); //Backend MEM version
                    fs.WriteUInt32(MEMI_TAG);
                }

                Log.Information(@"[AICORE] Stamped texture mod installation information on target");
            }
            catch (Exception e)
            {
                Log.Error($@"[AICORE] Error writing debug texture mod installation marker file: {e.Message}");
            }
        }

        internal void StripALOTInfo()
        {
#if DEBUG
            var markerPath = getALOTMarkerFilePath();

            try
            {
                using (FileStream fs = new FileStream(markerPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                {
                    fs.SeekEnd();
                    fs.Position -= 4;
                    fs.WriteUInt32(1234); //erase memi tag
                }

                Log.Information(@"[AICORE] Changed MEMI Tag for game to 1234.");
            }
            catch (Exception e)
            {
                Log.Error($@"[AICORE] Error stripping debug ALOT marker file for {Game}. {e.Message}");
            }
#endif
        }

        public void StampDebugALOTInfo()
        {
#if DEBUG
            TextureModInstallationInfo tmii = new TextureModInstallationInfo(8, 1, 0, 3);
            tmii.MarkerExtendedVersion = TextureModInstallationInfo.LATEST_TEXTURE_MOD_MARKER_VERSION;
            tmii.InstallationTimestamp = DateTime.Now;
            tmii.InstallerVersionFullName = "Debug Installer 3.2";
            var ran = new Random();
            var fileset = ManifestHandler.GetAllManifestFiles().Where(x => !(x is PreinstallMod) && ran.Next(3) == 0);
            tmii.InstalledTextureMods.AddRange(fileset.Select(x => new TextureModInstallationInfo.InstalledTextureMod(x)));
            StampTextureModificationInfo(tmii);
#endif
        }

        public bool HasALOTOrMEUITM()
        {
            var alotInfo = GetInstalledALOTInfo();
            return alotInfo != null && (alotInfo.ALOTVER > 0 || alotInfo.MEUITMVER > 0);
        }


        public ObservableCollectionExtended<InstalledExtraFile> ExtraFiles { get; } = new ObservableCollectionExtended<InstalledExtraFile>();

        /// <summary>
        /// Populates list of 'extra' items for the game. This includes things like dlls, and for ME1, config files
        /// </summary>
        public void PopulateExtras()
        {
            var exeDir = M3Directories.GetExecutableDirectory(this);
            var dlls = Directory.GetFiles(exeDir, @"*.dll").Select(x => Path.GetFileName(x));
            var expectedDlls = MEDirectories.VanillaDlls(this.Game);
            var extraDlls = dlls.Except(expectedDlls, StringComparer.InvariantCultureIgnoreCase);

            void notifyExtraFileDeleted(InstalledExtraFile ief)
            {
                ExtraFiles.Remove(ief);
            }

            ExtraFiles.ReplaceAll(extraDlls.Select(x => new InstalledExtraFile(Path.Combine(exeDir, x), InstalledExtraFile.EFileType.DLL, Game)));
        }

        public string NumASIModsInstalledText { get; private set; }

        //#if WPF
        //            public void PopulateASIInfo()
        //            {
        //                var asi = new ASIGame(this);
        //                var installedASIs = asi.GetInstalledASIMods(Game);
        //                if (installedASIs.Any())
        //                {
        //                    NumASIModsInstalledText = "This install has M3L.GetString(M3L.string_interp_asiStatus, installedASIs.Count);
        //                }
        //                else
        //                {
        //                    NumASIModsInstalledText = M3L.GetString(M3L.string_thisInstallationHasNoASIModsInstalled);
        //                }
        //            }

        //            public string Binkw32StatusText { get; private set; }

        //            public void PopulateBinkInfo()
        //            {
        //                if (Game != MEGame.ME1)
        //                {
        //                    Binkw32StatusText = Utilities.CheckIfBinkw32ASIIsInstalled(this) ? M3L.GetString(M3L.string_bypassInstalledASIAndDLCModsWillBeAbleToLoad) : M3L.GetString(M3L.string_bypassNotInstalledASIAndDLCModsWillBeUnableToLoad);
        //                }
        //                else
        //                {
        //                    Binkw32StatusText = Utilities.CheckIfBinkw32ASIIsInstalled(this) ? M3L.GetString(M3L.string_bypassInstalledASIModsWillBeAbleToLoad) : M3L.GetString(M3L.string_bypassNotInstalledASIModsWillBeUnableToLoad);
        //                }
        //            }
        //#endif

        private string getBinkPath()
        {

            if (Game == MEGame.ME1) return Path.Combine(TargetPath, "Binaries", "binkw32.dll");
            if (Game == MEGame.ME2) return Path.Combine(TargetPath, "Binaries", "binkw32.dll");
            if (Game == MEGame.ME3) return Path.Combine(TargetPath, "Binaries", "Win32", "binkw32.dll");
            return null;

        }

        public bool InstallBinkBypass()
        {
            var binkPath = getBinkPath();
            Log.Information($"[AICORE] Installing Binkw32 bypass for {Game} to {binkPath}");

            if (Game == MEGame.ME1)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me1.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me1.binkw23.dll", obinkPath, true);
            }
            else if (Game == MEGame.ME2)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me2.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me2.binkw23.dll", obinkPath, true);

            }
            else if (Game == MEGame.ME3)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "Win32", "binkw23.dll");
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me3.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me3.binkw23.dll", obinkPath, true);
            }
            else
            {
                Log.Error("[AICORE] Unknown game for gametarget (InstallBinkBypass)");
                return false;
            }

            Log.Information($"[AICORE] Installed Binkw32 bypass for {Game}");
            return true;
        }

        private const string ME1ASILoaderHash = "30660f25ab7f7435b9f3e1a08422411a";
        private const string ME2ASILoaderHash = "a5318e756893f6232284202c1196da13";
        private const string ME3ASILoaderHash = "1acccbdae34e29ca7a50951999ed80d5";

        public bool IsBinkBypassInstalled()
        {
            string binkPath = null;
            string expectedHash = null;
            if (Game == MEGame.ME1)
            {
                binkPath = Path.Combine(TargetPath, "Binaries", "binkw32.dll");
                expectedHash = ME1ASILoaderHash;
            }
            else if (Game == MEGame.ME2)
            {
                binkPath = Path.Combine(TargetPath, "Binaries", "binkw32.dll");
                expectedHash = ME2ASILoaderHash;
            }
            else if (Game == MEGame.ME3)
            {
                binkPath = Path.Combine(TargetPath, "Binaries", "Win32", "binkw32.dll");
                expectedHash = ME3ASILoaderHash;
            }

            if (File.Exists(binkPath))
            {
                return Utilities.CalculateMD5(binkPath) == expectedHash;
            }

            return false;
        }

        public List<InstalledASIMod> GetInstalledASIs()
        {
            List<InstalledASIMod> installedASIs = new List<InstalledASIMod>();
            try
            {
                string asiDirectory = M3Directories.GetASIPath(this);
                if (asiDirectory != null && Directory.Exists(TargetPath))
                {
                    if (!Directory.Exists(asiDirectory))
                    {
                        Directory.CreateDirectory(asiDirectory); //Create it, but we don't need it
                        return installedASIs; //It won't have anything in it if we are creating it
                    }

                    var asiFiles = Directory.GetFiles(asiDirectory, @"*.asi");
                    foreach (var asiFile in asiFiles)
                    {
                        var hash = Utilities.CalculateMD5(asiFile);
                        var matchingManifestASI = ASIManager.GetASIVersionByHash(hash, Game);
                        if (matchingManifestASI != null)
                        {
                            installedASIs.Add(new KnownInstalledASIMod(asiFile, hash, Game, matchingManifestASI));
                        }
                        else
                        {
                            installedASIs.Add(new UnknownInstalledASIMod(asiFile, hash, Game));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(@"[AICORE] Error fetching list of installed ASIs: " + e.Message);
            }

            return installedASIs;
        }
    }
}