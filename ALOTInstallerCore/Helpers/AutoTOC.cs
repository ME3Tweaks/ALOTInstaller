using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ALOTInstallerCore.ModManager;
using ALOTInstallerCore.ModManager.Objects;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Class for handling Mass Effect 3's AutoTOC. This one only works with unpacked DLC and basegame, and not packed SFARs
    /// </summary>
    public static class AutoTOC
    {
        // Might need changed for linux support?
        private const string SFAR_SUBPATH = @"CookedPCConsole\Default.sfar";

        /// <summary>
        /// Generates TOC files for the specified target. This implementation does not support TOCing packed SFARs.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="percentDoneCallback"></param>
        /// <returns></returns>
        public static bool RunTOCOnGameTarget(GameTarget target, Action<int> percentDoneCallback = null)
        {
            Log.Information(@"[AICORE] Autotocing game: " + target.TargetPath);

            //get toc target folders, ensuring we clean up the inputs a bit.
            string baseDir = Path.GetFullPath(Path.Combine(target.TargetPath, @"BIOGame"));
            string dlcDirRoot = M3Directories.GetDLCPath(target);
            if (!Directory.Exists(dlcDirRoot))
            {
                Log.Error(@"[AICORE] Specified game directory does not appear to be a Mass Effect 3 root game directory (DLC folder missing).");
                return false;
            }

            var tocTargets = (new DirectoryInfo(dlcDirRoot)).GetDirectories().Select(x => x.FullName).Where(x => Path.GetFileName(x).StartsWith(@"DLC_", StringComparison.OrdinalIgnoreCase)).ToList();
            tocTargets.Add(baseDir);
            tocTargets.Add(Path.Combine(target.TargetPath, @"BIOGame\Patches\PCConsole\Patch_001.sfar"));

            //Debug.WriteLine("Found TOC Targets:");
            tocTargets.ForEach(x => Debug.WriteLine(x));
            //Debug.WriteLine("=====Generating TOC Files=====");
            int done = 0;

            foreach (var tocTarget in tocTargets)
            {
                string sfar = Path.Combine(tocTarget, SFAR_SUBPATH);
                if (tocTarget.EndsWith(@".sfar"))
                {
                    // Not supported

                    //TestPatch
                    //var watch = Stopwatch.StartNew();
                    //DLCPackage dlc = new DLCPackage(tocTarget);
                    //var tocResult = dlc.UpdateTOCbin();
                    //watch.Stop();
                    //if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY)
                    //{
                    //    Log.Information(@"[AICORE] TOC is already up to date in {tocTarget}");
                    //}
                    //else if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATED)
                    //{
                    //    var elapsedMs = watch.ElapsedMilliseconds;
                    //    Log.Information(@"[AICORE] {tocTarget} - Ran SFAR TOC, took {elapsedMs}ms");
                    //}
                }
                else if (ME3Directory.OfficialDLCNames.ContainsKey(Path.GetFileName(tocTarget)))
                {
                    //Official DLC
                    if (File.Exists(sfar))
                    {
                        if (new FileInfo(sfar).Length == 32) //DLC is unpacked for sure
                        {
                            CreateUnpackedTOC(tocTarget);
                        }
                        else
                        {
                            //AutoTOC it - SFAR is not unpacked

                            // Not supported
                            //var watch = System.Diagnostics.Stopwatch.StartNew();

                            //DLCPackage dlc = new DLCPackage(sfar);
                            //var tocResult = dlc.UpdateTOCbin();
                            //watch.Stop();
                            //if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_ERROR_NO_ENTRIES)
                            //{
                            //    Log.Information(@"[AICORE] No DLC entries in SFAR... Suspicious. Creating empty TOC for {tocTarget}");
                            //    CreateUnpackedTOC(tocTarget);
                            //}
                            //else if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATE_NOT_NECESSARY)
                            //{
                            //    Log.Information(@"[AICORE] TOC is already up to date in {tocTarget}");
                            //}
                            //else if (tocResult == DLCPackage.DLCTOCUpdateResult.RESULT_UPDATED)
                            //{
                            //    var elapsedMs = watch.ElapsedMilliseconds;
                            //    Log.Information(@"[AICORE] {Path.GetFileName(tocTarget)} - Ran SFAR TOC, took {elapsedMs}ms");
                            //}
                        }
                    }

                }
                else
                {
                    //TOC it unpacked style
                    CreateUnpackedTOC(tocTarget);
                }

                done++;
                percentDoneCallback?.Invoke((int)Math.Floor(done * 100.0 / tocTargets.Count));
            }
            return true;
        }

        public static void CreateUnpackedTOC(string dlcDirectory)
        {
            Log.Information(@"[AICORE] Creating unpacked toc for " + dlcDirectory);
#if DEBUG
            if (dlcDirectory.Contains(@"DLC_CON_END") || dlcDirectory.Contains(@"DLC_EXP_Pack002"))
            {
                Debugger.Break();
                throw new Exception(@"ASSERT ERROR: CREATING UNPACKED TOC FOR OFFICIAL DLC!");
            }
#endif
            var watch = System.Diagnostics.Stopwatch.StartNew();
            MemoryStream ms = TOCCreator.CreateDLCTOCForDirectory(dlcDirectory, MEGame.ME3);
            if (ms != null)
            {
                string tocPath = Path.Combine(dlcDirectory, @"PCConsoleTOC.bin");
                File.WriteAllBytes(tocPath, ms.ToArray());
                ms.Close();
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                Log.Information($@"[AICORE] {Path.GetFileName(dlcDirectory)} - {dlcDirectory} Ran Unpacked TOC, took {elapsedMs}ms");
            }
            else
            {
                Log.Warning(@"[AICORE] Did not create TOC for " + dlcDirectory);
                watch.Stop();
            }
        }
    }
}
