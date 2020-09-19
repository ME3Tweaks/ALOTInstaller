using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ALOTInstallerCore.Helpers;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Defines the files and mode-specific assets for a specific mode
    /// </summary>
    public class ManifestModePackage
    {
        /// <summary>
        /// Files that are part of the mode's installation manifest.
        /// </summary>
        public List<ManifestFile> ManifestFiles = new List<ManifestFile>(60);

        /// <summary>
        /// The version of this mode's manifest
        /// </summary>
        public string ManifestVersion { get; set; }

        /// <summary>
        /// List of tutorials for this manifest mode
        /// </summary>
        public List<ManifestTutorial> Tutorials = new List<ManifestTutorial>(); //Still used?

        /// <summary>
        /// List of user supplied files for this mode
        /// </summary>
        public List<UserFile> UserFiles { get; } = new List<UserFile>();

        /// <summary>
        /// Description of this mode
        /// </summary>
        public string ModeDescription { get; set; } = "No rules. Install whatever you want"; //Defaults to 'None' description. Manifest loader will override this

        /// <summary>
        /// Sorts the manifest files by ui priority, author, name
        /// </summary>
        public void OrderManifestFiles()
        {
            ManifestFiles = ManifestFiles.OrderBy(p => p.UIPriority).ThenBy(o => o.Author).ThenBy(x => x.FriendlyName).ToList();
        }

        /// <summary>
        /// Attempts to add a user file. Returns a failure reason if the file could not be added, or null if it was.
        /// </summary>
        /// <param name="matchingPim"></param>
        public string AttemptAddUserFile(string filepath, Func<string, ApplicableGame?> getGame, out UserFile addedUserFile)
        {
            addedUserFile = null;
            if (UserFiles.Any(x => x.FullFilePath == filepath))
            {
                return "File is already added as a user file";
            }

            string description = "";
            ApplicableGame? game = null;
            if (Path.GetExtension(filepath) == ".mod")
            {
                var info = ModFileFormats.GetGameForMod(filepath);
                if (info.Usable)
                {
                    game = info.ApplicableGames;
                    description = info.Description;
                }
                else
                {
                    return info.Description; //why it failed
                }
            }
            else if (Path.GetExtension(filepath) == ".mem")
            {
                var info = ModFileFormats.GetInfoForMEMFile(filepath);
                if (info.Usable)
                {
                    game = info.ApplicableGames;
                    description = ""; //no description
                }
                else
                {
                    return info.Description; //why it failed
                }
            }

            if (game == null || game == ApplicableGame.None)
            {
                game = getGame.Invoke(filepath);
                if (game == null || game == ApplicableGame.None)
                    return "Skipped";
            }


            var ufi = new FileInfo(filepath);
            UserFile uf = new UserFile()
            {
                FileSize = ufi.Length,
                Filename = filepath, //Used for logging in some areas but otherwise will be unused
                FriendlyName = Path.GetFileNameWithoutExtension(filepath),
                FullFilePath = filepath,
                ApplicableGames = game.Value,
                Description = description,
                AlotVersionInfo = TextureModInstallationInfo.NoVersion
            };
            uf.UpdateReadyStatus();
            UserFiles.Add(uf);
            addedUserFile = uf;
            return null;
        }
    }
}
