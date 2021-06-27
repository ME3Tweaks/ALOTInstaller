using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.ModManager.Services;
using LegendaryExplorerCore.Packages;
using Serilog;

namespace ALOTInstallerCore.Objects.Manifest
{
    public abstract class CompatibilityPrecheck
    {
        public CompatibilityPrecheck(XElement element)
        {
            if (element.Attribute("me1")?.Value == "true")
                ApplicableGames |= ApplicableGame.ME1;
            if (element.Attribute("me2")?.Value == "true")
                ApplicableGames |= ApplicableGame.ME2;
            if (element.Attribute("me3")?.Value == "true")
                ApplicableGames |= ApplicableGame.ME3;
            IncompatibleMessage = element.Attribute("failuremessage").Value;
            FriendlyName = element.Attribute("friendlyname").Value;
            Mode = TryConvert.ToEnum(element.Attribute("mode")?.Value,ManifestMode.Invalid);
            if (ApplicableGames == ApplicableGame.None)
            {
                Log.Error($@"[AICORE] Compatibility precheck {FriendlyName} is incorrectly configured, it applies to no games!");
            }
        }


        /// <summary>
        /// Checks the game to see if this is a compatible configuration. Returns false if it isn't.
        /// </summary>
        /// <returns></returns>
        public abstract bool IsCompatibleConfig(GameTarget target);
        public ApplicableGame ApplicableGames { get; }
        public string IncompatibleMessage { get; }
        public string FriendlyName { get; }
        public ManifestMode Mode { get; }

    }

    public class PackageCompatibilityPrecheck : CompatibilityPrecheck
    {
        private string PackageFileRelativePath { get; }
        public string ExportMD5 { get; }
        public int ExportUIndex { get; }
        public bool FailIfPackageNotFound { get; }

        public PackageCompatibilityPrecheck(XElement element) : base(element)
        {
            PackageFileRelativePath = element.Attribute("packagerelativefilepath").Value;
            ExportUIndex = int.Parse(element.Attribute("exportuindex").Value);
            ExportMD5 = element.Attribute("exportmd5").Value;
            FailIfPackageNotFound = TryConvert.ToBool(element.Attribute("failifpackagenotfound")?.Value, false);
        }


        public override bool IsCompatibleConfig(GameTarget target)
        {
            if (!ApplicableGames.HasFlag(target.Game.ToApplicableGame())) return true; // This rule does not apply to this game
            var targetPackageFile = Path.GetFullPath(Path.Combine(target.TargetPath, PackageFileRelativePath));
            if (File.Exists(targetPackageFile))
            {
                using var package = MEPackageHandler.OpenMEPackage(targetPackageFile);
                var export = package.GetUExport(ExportUIndex);
                if (export == null)
                {
                    // Could not find export! This rule check failed.
                    Log.Information($@"[AICORE] {FriendlyName} Compatibility check did not pass: {PackageFileRelativePath} did not contain export with UIndex {ExportUIndex}");
                    return false;
                }

                var checkingExportMD5 = Utilities.CalculateMD5(new MemoryStream(export.Data));
                if (checkingExportMD5 != ExportMD5)
                {
                    Log.Information($@"[AICORE] {FriendlyName} Compatibility check did not pass: {PackageFileRelativePath}'s export {ExportUIndex} hash check failed. Expected hash {ExportMD5}, got {checkingExportMD5}");
                    return false;
                }
            }
            else if (FailIfPackageNotFound)
            {
                Log.Information($@"[AICORE] {FriendlyName} Compatibility check did not pass: {PackageFileRelativePath} was not found in target {target.TargetPath}");
                return false;
            }

            return true;
        }
    }

    public class FileCompatibilityPrecheck : CompatibilityPrecheck
    {
        public FileCompatibilityPrecheck(XElement element) : base(element)
        {
            FileRelativePath = element.Attribute("packagepath").Value;
            FileMD5 = element.Attribute("exportmd5").Value;
            FailIfFileNotFound = TryConvert.ToBool(element.Attribute("failiffilenotfound")?.Value, false);
            FailIfHashMatches = TryConvert.ToBool(element.Attribute("failifhashmatches")?.Value, false);
        }

        public string FileMD5 { get; }
        public string FileRelativePath { get; }
        public bool FailIfFileNotFound { get; }
        public bool FailIfHashMatches { get; }

        public override bool IsCompatibleConfig(GameTarget target)
        {
            if (!ApplicableGames.HasFlag(target.Game.ToApplicableGame())) return true; // This rule does not apply to this game
            var targetFile = Path.Combine(target.TargetPath, FileRelativePath);
            if (File.Exists(targetFile))
            {
                var localMd5 = Utilities.CalculateMD5(targetFile);
                var hashMatches = localMd5 == FileMD5;
                if (FailIfHashMatches && hashMatches)
                {
                    Log.Information($@"{FriendlyName} Compatibility check did not pass: {FileRelativePath} has hash {localMd5}");
                    return false;
                }
                if (!FailIfHashMatches && !hashMatches)
                {
                    Log.Information($@"[AICORE] {FriendlyName} Compatibility check did not pass: {FileRelativePath} has wrong hash. Expected {FileMD5}, got {localMd5}");
                    return false;
                }
            }
            else if (FailIfFileNotFound)
            {
                Log.Information($@"[AICORE] {FriendlyName} Compatibility check did not pass: {FileRelativePath} was not found in target {target.TargetPath}");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Checks if a DLC is installed. If it is this compatibility precheck will fail. These should be tied to manifest files to disable them automatically.
    /// </summary>
    public class DLCCompatibilityPrecheck : CompatibilityPrecheck
    {
        public string DLCName { get; }

        public DLCCompatibilityPrecheck(XElement element) : base(element)
        {
            DLCName = element.Attribute("dlcname").Value;
        }


        public override bool IsCompatibleConfig(GameTarget target)
        {
            if (!ApplicableGames.HasFlag(target.Game.ToApplicableGame())) return true; // This rule does not apply to this game
            var compatible = !VanillaDatabaseService.GetInstalledDLCMods(target).Any(x => x.Equals(DLCName, StringComparison.InvariantCultureIgnoreCase));
            if (!compatible)
            {
                Log.Information($@"[AICORE] {FriendlyName} Compatibility check did not pass: {DLCName} DLC was found in target {target.TargetPath}");
            }
            return compatible;
        }
    }
}
