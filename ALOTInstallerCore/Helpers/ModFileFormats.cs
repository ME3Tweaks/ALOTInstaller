using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ALOTInstallerCore.ModManager.GameDirectories;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerCore.Helpers
{
    public class ModFileInfo
    {
        public ApplicableGame ApplicableGames { get; set; }
        public string Description { get; set; }
        public bool Usable { get; set; }
        public List<string> RequiredFiles { get; set; } = new List<string>();
    }

    public static class ModFileFormats
    {

        public static ModFileInfo GetInfoForMEMFile(string file)
        {
            try
            {
                using var memFile = File.OpenRead(file);
                var magic = memFile.ReadStringASCII(4);
                if (magic != "TMOD")
                {
                    return new ModFileInfo()
                    {
                        ApplicableGames = ApplicableGame.None,
                        Description = "Invalid MEM file: Bad magic header",
                        Usable = false
                    };
                }
                var version = memFile.ReadInt32();
                var gameIdOffset = memFile.ReadInt64();
                memFile.Position = gameIdOffset;
                var gameId = memFile.ReadInt32();
                ApplicableGame game = ApplicableGame.None;

                if (gameId == 1) game = ApplicableGame.ME1;
                if (gameId == 2) game = ApplicableGame.ME2;
                if (gameId == 3) game = ApplicableGame.ME3;
                if (game == ApplicableGame.None)
                {
                    return new ModFileInfo()
                    {
                        ApplicableGames = ApplicableGame.None,
                        Description = $"Invalid MEM file: Bad game ID",
                        Usable = false
                    };
                }

                var target = Locations.GetTarget(game.ApplicableGameToMEGame());
                if (target == null)
                {
                    return new ModFileInfo()
                    {
                        ApplicableGames = ApplicableGame.None,
                        Description = $"Target game ({game.ApplicableGameToMEGame()}) is not installed",
                        Usable = false
                    };
                }

                return new ModFileInfo()
                {
                    ApplicableGames = game,
                    Usable = true
                };

            }
            catch (Exception e)
            {
                return new ModFileInfo()
                {
                    ApplicableGames = ApplicableGame.None,
                    Description = e.Message,
                    Usable = false
                };
            }
        }

        public static ModFileInfo GetGameForMod(string file)
        {
            try
            {
                using var modFile = File.OpenRead(file);
                var len = modFile.ReadInt32(); //first 4 bytes
                var version = modFile.ReadStringASCIINull();
                modFile.SeekBegin();
                if (version.Length >= 5) // "modern" .mod
                {
                    //Re-read the version length
                    version = modFile.ReadUnrealString();
                }
                var numEntries = modFile.ReadUInt32();
                string desc = modFile.ReadUnrealString();
                var script = modFile.ReadUnrealString().Split("\n").Select(x => x.Trim()).ToList();
                ApplicableGame game = ApplicableGame.None;
                if (script.Any(x => x.StartsWith("using ME1Explorer")))
                {
                    game |= ApplicableGame.ME1;
                }
                else if (script.Any(x => x.StartsWith("using ME2Explorer")))
                {
                    game |= ApplicableGame.ME2;
                }
                else if (script.Any(x => x.StartsWith("using ME3Explorer")))
                {
                    game |= ApplicableGame.ME3;
                }

                var target = Locations.GetTarget(game.ApplicableGameToMEGame());
                if (target == null)
                {
                    return new ModFileInfo()
                    {
                        ApplicableGames = ApplicableGame.None,
                        Description = $"Target game ({game.ApplicableGameToMEGame()}) is not installed",
                        Usable = false
                    };
                }

                var biogame = MEDirectories.BioGamePath(target);
                foreach (var pcc in script.Where(x => x.StartsWith("pccs.Add(")))
                {
                    var subBioPath = pcc.Substring("pccs.Add(\"".Length);
                    subBioPath = subBioPath.Substring(0, subBioPath.Length - 3);
                    var targetFile = Path.Combine(biogame, subBioPath);
                    if (!File.Exists(targetFile))
                    {
                        return new ModFileInfo()
                        {
                            ApplicableGames = ApplicableGame.None,
                            Description = $"Target file doesn't exist: {subBioPath}",
                            Usable = false
                        };
                    }
                }

                return new ModFileInfo()
                {
                    ApplicableGames = game,
                    Description = desc,
                    Usable = true
                };
            }
            catch (Exception e)
            {
                return new ModFileInfo()
                {
                    ApplicableGames = ApplicableGame.None,
                    Description = e.Message,
                    Usable = false
                };
            }


            //string path = "";
            //if (desc.Contains("Binary Replacement"))
            //{
            //    try
            //    {
            //        ParseME3xBinaryScriptMod(scriptLegacy, ref package, ref mod.exportId, ref path);
            //        if (mod.exportId == -1 || package == "" || path == "")
            //        {
            //            // NOT COMPATIBLE
            //            return ApplicableGame.None;
            //        }
            //    }
            //    catch
            //    {
            //        // NOT COMPATIBLE
            //        return ApplicableGame.None;
            //    }
            //    mod.packagePath = Path.Combine(path, package);
            //    mod.binaryModType = 1;
            //    len = modFile.ReadInt32();
            //    mod.data = modFile.ReadToBuffer(len);
            //}
            //else
            //{
            //    modFile.SeekBegin();
            //    len = modFile.ReadInt32();
            //    version = modFile.ReadStringASCII(len); // version
            //}

        }
    }
}