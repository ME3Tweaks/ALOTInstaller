using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ALOTInstallerCore.Helpers.AppSettings;
using CliWrap;
using CliWrap.EventStream;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Gammtek.Extensions;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;

namespace ALOTInstallerCore.Helpers
{
    [Flags]
    public enum LodSetting
    {
        Vanilla = 0,
        TwoK = 1,
        FourK = 2,
        SoftShadows = 4,
    }


    /// <summary>
    /// Utility class for interacting with MEM. Calls must be run on a background thread of
    /// </summary>
    public static class MEMIPCHandler
    {

        #region Static Property Changed

        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public static event PropertyChangedEventHandler StaticBackupStateChanged;

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion

        private static short _memVersion = -1;
        public static short MassEffectModderNoGuiVersion
        {
            get => _memVersion;
            set => SetProperty(ref _memVersion, value);
        }

        // gotta love C#
        private static ConcurrentDictionary<int, int> ActiveMEMProcessIDs = new ConcurrentDictionary<int, int>();

        /// <summary>
        /// Flag to prevent MEM IPC handler from firing further. This can happen when app is shutting down and install thread is running
        /// </summary>
        private static bool SuppressFurtherMEMLaunches;

        /// <summary>
        /// Kills all known active instances of MEM.
        /// </summary>
        public static void KillAllActiveMEMInstances()
        {
            SuppressFurtherMEMLaunches = true;
            foreach (var v in ActiveMEMProcessIDs.Keys.ToList()) // To list as this may try to be concurrently modified. I'm not sure how that works in a concurrent dictionary but removing keys in the loop func will probably do that.
            {
                try
                {
                    Log.Information($@"[AICORE] Killing MassEffectModderNoGui process {v}");
                    Process.GetProcessById(v)?.Kill();
                }
                catch { } //We don't really care if this fails.
            }
        }

        /// <summary>
        /// Tests if MEM is working and available
        /// </summary>
        /// <returns></returns>
        public static bool TestWorkingMEM()
        {
            try
            {
                var version = MEMIPCHandler.GetMemVersion(true);
                return version > 421;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the version number for MEM, or -1 if it couldn't be retrieved. The result is cached into the variable MassEffectModderNoGuiVerison
        /// </summary>
        /// <returns></returns>
        public static short GetMemVersion(bool invalidateCache = false)
        {
            if (invalidateCache) MassEffectModderNoGuiVersion = -1;
            if (MassEffectModderNoGuiVersion <= 0 && File.Exists(Locations.MEMPath()) && new FileInfo(Locations.MEMPath()).Length > 0)
            {
                // If the current version doesn't support the --version --ipc, we just assume it is -1.
                MEMIPCHandler.RunMEMIPCUntilExit("--version --ipc", ipcCallback: (command, param) =>
                {
                    if (command == "VERSION")
                    {
                        MassEffectModderNoGuiVersion = short.Parse(param);
                    }
                });
            }

            return MassEffectModderNoGuiVersion;
        }

        public static int ExtractArchiveToDirectory(string inputArchive, string outputFolder)
        {
            int extractcode = -1;
            Directory.CreateDirectory(outputFolder);
            MEMIPCHandler.RunMEMIPCUntilExit($"--unpack-archive --input \"{inputArchive}\" --output \"{outputFolder}\" --ipc",
                null,
                null,
                x => Log.Error($"[AICORE] StdError extracting {inputArchive}: {x}"),
                x => extractcode = x);
            return extractcode;
        }

        /// <summary>
        /// Verifies a game against the MEM MD5 database
        /// </summary>
        /// <param name="game"></param>
        /// <param name="applicationStarted"></param>
        /// <param name="ipcCallback"></param>
        /// <param name="applicationStdErr"></param>
        /// <param name="applicationExited"></param>
        /// <param name="cancellationToken"></param>
        public static void VerifyVanilla(MEGame game, Action<int> applicationStarted = null,
            Action<string, string> ipcCallback = null, Action<string> applicationStdErr = null,
            Action<int> applicationExited = null, CancellationToken cancellationToken = default)
        {
            RunMEMIPCUntilExit($"--check-game-data-vanilla --gameid {game.ToGameNum()} --ipc", applicationStarted, ipcCallback, applicationStdErr, applicationExited, null, cancellationToken);
        }

        public static void RunMEMIPCUntilExit(string arguments,
            Action<int> applicationStarted = null,
            Action<string, string> ipcCallback = null,
            Action<string> applicationStdErr = null,
            Action<int> applicationExited = null,
            Action<string> setMEMCrashLog = null,
            CancellationToken cancellationToken = default)
        {
            if (Settings.DebugLogs)
            {
                arguments += " --debug-logs";
            }
            object lockObject = new object();
            void appStart(int processID)
            {
                applicationStarted?.Invoke(processID);
                // This might need to be waited on after method is called.
                Debug.WriteLine(@"Process launched. Process ID: " + processID);
            }
            void appExited(int code)
            {
                Debug.WriteLine($"Process exited with code {code}");
                applicationExited?.Invoke(code);
                lock (lockObject)
                {
                    Monitor.Pulse(lockObject);
                }
            }

            StringBuilder crashLogBuilder = new StringBuilder();

            void memCrashLogOutput(string str)
            {
                crashLogBuilder.AppendLine(str);
            }

            // Run MEM
            MEMIPCHandler.RunMEMIPC(arguments, appStart, ipcCallback, applicationStdErr, appExited, memCrashLogOutput,
                cancellationToken);

            // Wait until exit
            lock (lockObject)
            {
                Monitor.Wait(lockObject);
            }

            if (crashLogBuilder.Length > 0)
            {
                setMEMCrashLog?.Invoke(crashLogBuilder.ToString());
            }
        }

        private static async void RunMEMIPC(string arguments, Action<int> applicationStarted = null, Action<string, string> ipcCallback = null, Action<string> applicationStdErr = null, Action<int> applicationExited = null, Action<string> memCrashLine = null, CancellationToken cancellationToken = default)
        {
            if (SuppressFurtherMEMLaunches) return;
            if (!File.Exists(Locations.MEMPath()))
            {
                Task.Run(() =>
                {
                    // this is so the locks still work
                    Thread.Sleep(500);
                    applicationExited?.Invoke(-1);
                });
                Log.Error(@"[AICORE] Can't run MassEffectModderNoGui: It doesn't exist! You may need to install the support package since it didn't seem to auto download");
                return; //Can't run if it doesn't exist!
            }
            bool exceptionOcurred = false;
            DateTime lastCacheoutput = DateTime.Now;
            void internalHandleIPC(string command, string parm)
            {
                switch (command)
                {
                    case "CACHE_USAGE":
                        if (DateTime.Now > (lastCacheoutput.AddSeconds(10)))
                        {
                            Log.Information($"[AICORE] MEM cache usage: {FileSize.FormatSize(long.Parse(parm))}");
                            lastCacheoutput = DateTime.Now;
                        }
                        break;
                    case "EXCEPTION_OCCURRED": //An exception has occurred and MEM is going to crash
                        exceptionOcurred = true;
                        ipcCallback?.Invoke(command, parm);
                        break;
                    default:
                        if (Settings.DebugLogs)
                        {
                            Log.Debug($@"[AICORE] Mem Output: {command} {parm}");
                        }
                        ipcCallback?.Invoke(command, parm);
                        break;
                }
            }

            // No validation. Make sure exit code is checked in the calling process.
            var cmd = Cli.Wrap(Locations.MEMPath()).WithArguments(arguments).WithValidation(CommandResultValidation.None);
            Log.Information($"[AICORE] Invoking MEM with IPC: {Locations.MEMPath()} {arguments}");
            int localProcessId = -1;
#if WINDOWS
            await foreach (var cmdEvent in cmd.ListenAsync(Encoding.Unicode, cancellationToken))
#elif LINUX
            await foreach (var cmdEvent in cmd.ListenAsync(Encoding.UTF8, cancellationToken))
#endif
            {
                switch (cmdEvent)
                {
                    case StartedCommandEvent started:
                        ActiveMEMProcessIDs.TryAdd(started.ProcessId, started.ProcessId);
                        applicationStarted?.Invoke(started.ProcessId);
                        localProcessId = started.ProcessId;
                        break;
                    case StandardOutputCommandEvent stdOut:
#if DEBUG
                        if (!stdOut.Text.StartsWith("[IPC]CACHE_USAGE"))
                        {
                            Debug.WriteLine(stdOut.Text);
                        }
#endif
                        if (stdOut.Text.StartsWith(@"[IPC]"))
                        {
                            var ipc = breakdownIPC(stdOut.Text);
                            internalHandleIPC(ipc.command, ipc.param);
                        }
                        else
                        {
                            if (exceptionOcurred)
                            {
                                Log.Fatal($"[AICORE] {stdOut.Text}");
                                memCrashLine?.Invoke(stdOut.Text);
                            }
                        }
                        break;
                    case StandardErrorCommandEvent stdErr:
                        Debug.WriteLine("STDERR " + stdErr.Text);
                        if (exceptionOcurred)
                        {
                            Log.Fatal($"[AICORE] {stdErr.Text}");
                        }
                        else
                        {
                            applicationStdErr?.Invoke(stdErr.Text);
                        }
                        break;
                    case ExitedCommandEvent exited:
                        ActiveMEMProcessIDs.TryRemove(localProcessId, out _);
                        applicationExited?.Invoke(exited.ExitCode);
                        break;
                }
            }
        }

        /// <summary>
        /// Converts MEM IPC output to command, param for handling. This method assumes string starts with [IPC] always.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static (string command, string param) breakdownIPC(string str)
        {
            string command = str.Substring(5);
            int endOfCommand = command.IndexOf(' ');
            if (endOfCommand >= 0)
            {
                command = command.Substring(0, endOfCommand);
            }

            string param = str.Substring(endOfCommand + 5).Trim();
            return (command, param);
        }

        /// <summary>
        /// Sets the path MEM will use for the specified game
        /// </summary>
        /// <param name="targetGame"></param>
        /// <param name="targetPath"></param>
        /// <returns></returns>
        public static bool SetGamePath(MEGame targetGame, string targetPath)
        {
            int exitcode = 0;
            string args = $"--set-game-data-path --gameid {targetGame.ToGameNum()} --path \"{targetPath}\"";
            MEMIPCHandler.RunMEMIPCUntilExit(args, applicationExited: x => exitcode = x);
            if (exitcode != 0)
            {
                Log.Error($"[AICORE] Non-zero MassEffectModderNoGui exit code setting game path: {exitcode}");
            }
            return exitcode == 0;
        }

        /// <summary>
        /// Sets the LODs as specified in the setting bitmask with MEM for the specified game
        /// </summary>
        /// <param name="game"></param>
        /// <param name="setting"></param>
        /// <returns></returns>
        public static bool SetLODs(MEGame game, LodSetting setting)
        {
            Log.Information($@"[AICORE] Settings LODs for {game}. Setting: {setting}");

            bool configFileReadOnly = false;
            if (game == MEGame.ME1)
            {
                try
                {
                    // Get read only state for config file. It seems sometimes they get set read only.
                    FileInfo fi = new FileInfo(MEDirectories.GetLODConfigFile(game));
                    configFileReadOnly = fi.IsReadOnly;
                    if (configFileReadOnly)
                    {
                        Log.Information(@"[AICORE] Removing read only flag from ME1 bioengine.ini");
                        fi.IsReadOnly = false; //clear read only. might happen on some binkw32 in archives, maybe
                    }
                }
                catch (Exception e)
                {
                    Log.Error($@"[AICORE] Error removing readonly flag from ME1 bioengine.ini: {e.Message}");
                }
            }

            string args = $"--apply-lods-gfx --gameid {game.ToGameNum()}";
            if (setting.HasFlag(LodSetting.SoftShadows))
            {
                args += " --soft-shadows-mode --meuitm-mode";
            }

            if (setting.HasFlag(LodSetting.TwoK))
            {
                args += " --limit-2k";
            }
            else if (setting.HasFlag(LodSetting.FourK))
            {
                // Nothing
            }
            else if (setting == LodSetting.Vanilla)
            {
                // Remove/Reset LODs
                args = $"--remove-lods --gameid {game.ToGameNum()}";
            }

            int exitcode = -1;
            if (game == MEGame.ME1 || setting != LodSetting.Vanilla) // me1 must have them explicitly set. Set LODs with MEM if setting them to not vanilla
            {
                // We don't care about IPC on this
                MEMIPCHandler.RunMEMIPCUntilExit(args,
                    null,
                    null,
                    x => Log.Error($"[AICORE] StdError setting LODs: {x}"),
                    x => exitcode = x); //Change to catch exit code of non zero.        
            }
            else
            {
                // Just write out the gamersettings file without TextureLODSettings (removing LODs - setting vanilla)
                DuplicatingIni di = DuplicatingIni.LoadIni(MEDirectories.GetLODConfigFile(game));
                var tls = di.Sections.FirstOrDefault(x => x.Header == "TextureLODSettings");
                if (tls != null)
                {
                    di.Sections.Remove(tls);
                }
                File.WriteAllText(MEDirectories.GetLODConfigFile(game), di.ToString());
                exitcode = 0;
            }

            if (configFileReadOnly)
            {
                try
                {
                    Log.Information(@"[AICORE] Re-setting the read only flag on ME1 bioengine.ini");
                    FileInfo fi = new FileInfo(MEDirectories.GetLODConfigFile(game));
                    fi.IsReadOnly = configFileReadOnly;
                }
                catch (Exception e)
                {
                    Log.Error($@"[AICORE] Error re-setting readonly flag from ME1 bioengine.ini: {e.Message}");
                }
            }

            if (exitcode != 0)
            {
                Log.Error($"[AICORE] MassEffectModderNoGui had error setting LODs, exited with code {exitcode}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets list of files in an archive
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static List<string> GetFileListing(string file)
        {
            string args = $"--list-archive --input \"{file}\" --ipc";
            List<string> fileListing = new List<string>();

            int exitcode = -1;
            MEMIPCHandler.RunMEMIPCUntilExit(args,
                null,
                (command, param) =>
                {
                    if (command == "FILENAME")
                    {
                        fileListing.Add(param);
                    }
                },
                x => Log.Error($"[AICORE] StdError getting file listing for file {file}: {x}"),
                x => exitcode = x); //Change to catch exit code of non zero.        
            if (exitcode != 0)
            {
                Log.Error($"[AICORE] MassEffectModderNoGui had error getting file listing of archive {file}, exit code {exitcode}");
            }
            return fileListing;
        }

        /// <summary>
        /// Fetches the list of LODs for the specified game
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetLODs(MEGame game)
        {
            Dictionary<string, string> lods = new Dictionary<string, string>();
            var args = $@"--print-lods --gameid {game.ToGameNum()} --ipc";
            int exitcode = -1;
            MEMIPCHandler.RunMEMIPCUntilExit(args, ipcCallback: (command, param) =>
                {
                    switch (command)
                    {
                        case @"LODLINE":
                            try
                            {
                                var lodSplit = param.Split(@"=");
                                lods[lodSplit[0]] = param.Substring(lodSplit[0].Length + 1);
                            }
                            catch (Exception e)
                            {
                                CoreCrashes.TrackError2(new Exception("Error printing MEM LODs over IPC", e), new Dictionary<string, string>()
                                {
                                    {"Command",command},
                                    {"Param", param}
                                });
                                lods[param] = $"ERROR SPLITTING STRING: {e.Message}. ";
                            }
                            break;
                        default:
                            //Debug.WriteLine(@"oof?");
                            break;
                    }
                },
                applicationExited: x => exitcode = x
            );
            if (exitcode != 0)
            {
                Log.Error($"[AICORE] Error fetching LODs for {game}, exit code {exitcode}");
                return null; // Error getting LODs
            }

            return lods;
        }

        /// <summary>
        /// Used to pass data back to installer core. DO NOT CHANGE VALUES AS
        /// THEY ARE INDIRECTLY REFERENCED
        /// </summary>
        public enum GameDirPath
        {
            ME1GamePath,
            ME1ConfigPath,
            ME2GamePath,
            ME2ConfigPath,
            ME3GamePath,
            ME3ConfigPath,
        }

        /// <summary>
        /// Returns location of the game and config paths (on linux) as defined by MEM, or null if game can't be found.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static Dictionary<GameDirPath, string> GetGameLocations()
        {
            Dictionary<GameDirPath, string> result = new Dictionary<GameDirPath, string>();
            MEMIPCHandler.RunMEMIPCUntilExit($"--get-game-paths --ipc", ipcCallback: (command, param) =>
            {
                var spitIndex = param.IndexOf(' ');
                if (spitIndex < 0) return; // This is nothing
                var gameId = param.Substring(0, spitIndex);
                var path = Path.GetFullPath(param.Substring(spitIndex + 1, param.Length - (spitIndex + 1)));
                switch (command)
                {
                    case "GAMEPATH":
                        {
                            var keyname = Enum.Parse<GameDirPath>($"ME{gameId}GamePath");
                            if (param.Length > 1)
                            {
                                result[keyname] = path;
                            }
                            else
                            {
                                result[keyname] = null;
                            }
                            break;
                        }
                    case "GAMECONFIGPATH":
                        {
                            var keyname = Enum.Parse<GameDirPath>($"ME{gameId}ConfigPath");
                            if (param.Length > 1)
                            {
                                result[keyname] = path;
                            }
                            else
                            {
                                result[keyname] = null;
                            }
                            break;
                        }
                }
            });
            return result;
        }

#if !WINDOWS
        public static bool SetConfigPath(MEGame game, string itemValue)
        {
            int exitcode = 0;
            string args = $"--set-game-user-path --gameid {game.ToGameNum()} --path \"{itemValue}\"";
            MEMIPCHandler.RunMEMIPCUntilExit(args, applicationExited: x => exitcode = x);
            if (exitcode != 0)
            {
                Log.Error($"[AICORE] Non-zero MassEffectModderNoGui exit code setting game config path: {exitcode}");
            }
            return exitcode == 0;
        }
#endif
    }
}
