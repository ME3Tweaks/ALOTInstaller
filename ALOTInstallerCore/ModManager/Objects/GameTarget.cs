using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.asi;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.ModManager.Services;
using ALOTInstallerCore.Objects;
using MassEffectModManagerCore.modmanager.asi;
using Serilog;
using Path = System.IO.Path;

namespace ALOTInstallerCore.ModManager.Objects
{
    public class GameTarget : IEqualityComparer<GameTarget>, INotifyPropertyChanged
    {
        public const uint MEMI_TAG = 0x494D454D;

        public event PropertyChangedEventHandler PropertyChanged;

        public Enums.MEGame Game { get; }
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

        public bool Supported => GameSource != null;
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

        public GameTarget(Enums.MEGame game, string targetRootPath, bool currentRegistryActive, bool isCustomOption = false)
        {
            this.Game = game;
            this.RegistryActive = currentRegistryActive;
            this.IsCustomOption = isCustomOption;
            this.TargetPath = targetRootPath.TrimEnd('\\');
            ReloadGameTarget(true, false);
        }

        public void ReloadGameTarget(bool logSource = true, bool forceLodUpdate = false)
        {
            if (Game != Enums.MEGame.Unknown && !IsCustomOption)
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
                        if (GameSource.Contains(@"Origin") && Game == Enums.MEGame.ME3)
                        {
                            // Check for steam
                            if (Directory.Exists(Path.Combine(TargetPath, @"__overlay")))
                            {
                                GameSource += @" (Steam version)";
                            }
                        }

                        CLog.Information(@"[AICORE] Source: " + GameSource, logSource);
                    }

                    IsPolishME1 = Game == Enums.MEGame.ME1 && File.Exists(Path.Combine(TargetPath, @"BioGame", @"CookedPC", @"Movies", @"niebieska_pl.bik"));
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
                if (Game == Enums.MEGame.ME1)
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

        public TextureModInstallationInfo GetInstalledALOTInfo()
        {
            string gamePath = getALOTMarkerFilePath();
            if (gamePath != null && File.Exists(gamePath))
            {
                try
                {
                    using FileStream fs = new FileStream(gamePath, System.IO.FileMode.Open, FileAccess.Read);
                    fs.SeekEnd();
                    long endPos = fs.Position;
                    fs.Position = endPos - 4;
                    uint memi = fs.ReadUInt32();

                    if (memi == MEMI_TAG)
                    {
                        //ALOT has been installed
                        fs.Position = endPos - 8;
                        short installerVersionUsed = fs.ReadInt16();
                        short memVersionUsed = fs.ReadInt16();
                        fs.Position -= 4; //roll back so we can read this whole thing as 4 bytes
                        int preMemi4Bytes = fs.ReadInt32();
                        int perGameFinal4Bytes = -20;
                        switch (Game)
                        {
                            case Enums.MEGame.ME1:
                                perGameFinal4Bytes = 0;
                                break;
                            case Enums.MEGame.ME2:
                                perGameFinal4Bytes = 4352;
                                break;
                            case Enums.MEGame.ME3:
                                perGameFinal4Bytes = 16777472;
                                break;
                        }

                        if (preMemi4Bytes != perGameFinal4Bytes) //default bytes before 178 MEMI Format
                        {
                            fs.Position = endPos - 12;
                            short ALOTVER = fs.ReadInt16();
                            byte ALOTUPDATEVER = (byte)fs.ReadByte();
                            byte ALOTHOTFIXVER = (byte)fs.ReadByte();

                            //unused for now
                            fs.Position = endPos - 16;
                            int MEUITMVER = fs.ReadInt32();

                            return new TextureModInstallationInfo(ALOTVER, ALOTUPDATEVER, ALOTHOTFIXVER, MEUITMVER, installerVersionUsed, memVersionUsed);
                        }
                        else
                        {
                            return new TextureModInstallationInfo(0, 0, 0, 0); //MEMI tag but no info we know of
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"[AICORE] Error reading ALOT marker file for {Game}. ALOT Info will be returned as null (nothing installed). " + e.Message);
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
            return MEDirectories.ALOTMarkerPath(this);
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
            var dlcDir = MEDirectories.DLCPath(this);
            var installedMods = MEDirectories.GetInstalledDLC(this, includeDisabled).Where(x => !MEDirectories.OfficialDLC(Game).Contains(x.TrimStart('x'), StringComparer.InvariantCultureIgnoreCase));
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
                case Enums.MEGame.ME1:
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
                case Enums.MEGame.ME2:
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
                case Enums.MEGame.ME3:
                    validationFiles = new[]
                    {
                            Path.Combine(TargetPath, @"Binaries", @"win32", @"MassEffect3.exe"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Startup.pcc"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Coalesced.bin"),
                            Path.Combine(TargetPath, @"BioGame", @"Patches", @"PCConsole", @"Patch_001.sfar"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"Textures.tfc"),
                            Path.Combine(TargetPath, @"BioGame", @"CookedPCConsole", @"citwrd_rp1_bailey_m_D_Int.afc")
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
                using (FileStream fs = new FileStream(markerPath, System.IO.FileMode.Open, FileAccess.ReadWrite))
                {
                    // MARKER FILE FORMAT
                    // When writing marker, the end of the file is appended with the following data:

                    // INT MEUITM VERSION
                    // SHORT ALOT MAJOR
                    // BYTE ALOT UPDATE
                    // BYTE ALOT HOTFIX (NOT USED)
                    // SHORT MEM VERSION USED
                    // SHORT INSTALLER VERSION USED
                    // BYTE "MEMI" ASCII

                    fs.SeekEnd();
                    fs.WriteInt32(tmii.MEUITMVER); //meuitm
                    fs.WriteInt16(tmii.ALOTVER); //major
                    fs.WriteByte(tmii.ALOTUPDATEVER); //minor
                    fs.WriteByte(tmii.ALOTHOTFIXVER); //hotfix
                    fs.WriteInt16(tmii.ALOT_INSTALLER_VERSION_USED); //installer version
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
            var exeDir = MEDirectories.ExecutableDirectory(this);
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
        //                if (Game != Enums.MEGame.ME1)
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

            if (Game == Enums.MEGame.ME1) return Path.Combine(TargetPath, "Binaries", "binkw32.dll");
            if (Game == Enums.MEGame.ME2) return Path.Combine(TargetPath, "Binaries", "binkw32.dll");
            if (Game == Enums.MEGame.ME3) return Path.Combine(TargetPath, "Binaries", "win32", "binkw32.dll");
            return null;

        }

        public bool InstallBinkBypass()
        {
            var binkPath = getBinkPath();
            Log.Information($"[AICORE] Installing Binkw32 bypass for {Game} to {binkPath}");

            if (Game == Enums.MEGame.ME1)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me1.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me1.binkw23.dll", obinkPath, true);
            }
            else if (Game == Enums.MEGame.ME2)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "binkw23.dll");
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me2.binkw32.dll", binkPath, true);
                Utilities.ExtractInternalFile("ALOTInstallerCore.ModManager.binkw32.me2.binkw23.dll", obinkPath, true);

            }
            else if (Game == Enums.MEGame.ME3)
            {
                var obinkPath = Path.Combine(TargetPath, "Binaries", "win32", "binkw23.dll");
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
            if (Game == Enums.MEGame.ME1)
            {
                binkPath = Path.Combine(TargetPath, "Binaries", "binkw32.dll");
                expectedHash = ME1ASILoaderHash;
            }
            else if (Game == Enums.MEGame.ME2)
            {
                binkPath = Path.Combine(TargetPath, "Binaries", "binkw32.dll");
                expectedHash = ME2ASILoaderHash;
            }
            else if (Game == Enums.MEGame.ME3)
            {
                binkPath = Path.Combine(TargetPath, "Binaries", "win32", "binkw32.dll");
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
                string asiDirectory = MEDirectories.ASIPath(this);
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