﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using Serilog;

namespace ALOTInstallerCore.Steps
{
    /// <summary>
    /// This step calculates the available build options that should be presented to the user depending on the current game state and the library.
    /// </summary>
    public class InstallOptionsStep
    {
        public enum OptionState
        {
            /// <summary>
            /// The item should be visible to the user to indicate it's an option but it should be disabled because it should not be selectable in the current state
            /// </summary>
            DisabledVisible,

            /// <summary>
            /// The item should be visible to the user, but should be unchecked by default, indicating this is an optional item to install right now
            /// </summary>
            UncheckedVisible,

            /// <summary>
            /// The item should be visible to the user, but it should be checked by default, indicating this is a recommended item to install
            /// </summary>
            CheckedVisible,

            /// <summary>
            /// The item should be visible to the end user, but it is forcibly checked and cannot be changed, indicating this option is required
            /// </summary>
            ForceCheckedVisible
        }

        public enum InstallOption
        {
            ALOT,
            ALOTUpdate,
            ALOTAddon,
            MEUITM,
            UserFiles
        }

        public static Dictionary<InstallOption, (OptionState state, string reasonForState)> CalculateInstallOptions(
            GameTarget gameTarget, ManifestMode manifestMode, List<InstallerFile> filesForTarget)
        {
            var targetAlotInfo = gameTarget.GetInstalledALOTInfo();
            var options = new Dictionary<InstallOption, (OptionState, string)>();
            if (manifestMode == ManifestMode.None)
            {
                if (filesForTarget.Any(x => x is UserFile && x.Ready))
                {
                    options[InstallOption.UserFiles] = (OptionState.ForceCheckedVisible, null);
                }
                else
                {
                    options[InstallOption.UserFiles] = (OptionState.DisabledVisible, "User files have been added for install but none of them are ready to install");
                }

                return options;
            }

            if (manifestMode == ManifestMode.MEUITM)
            {
                // MEUITM mode requires MEUITM file ready

                var meuitmFile = filesForTarget.FirstOrDefault(x =>
                    x.AlotVersionInfo.MEUITMVER > 0);
                if (meuitmFile == null)
                {
                    // Not in manifest...?
                    Log.Error("Manifest for MEUITM mode is missing MEUITM!");
                    return options;
                }

                int meuitmFileVer = meuitmFile.AlotVersionInfo.MEUITMVER;
                if (targetAlotInfo != null && targetAlotInfo.MEUITMVER > 0)
                {
                    // Has some version of MEUITM installed
                    if (targetAlotInfo.MEUITMVER != meuitmFileVer)
                    {
                        options[InstallOption.MEUITM] = (OptionState.DisabledVisible,
                            "Cannot install a different version of MEUITM on top of existing installed version");
                    }
                    else if (meuitmFile.Ready)
                    {
                        options[InstallOption.MEUITM] = (OptionState.UncheckedVisible, "MEUITM already installed");
                    }
                    else
                    {
                        options[InstallOption.MEUITM] = (OptionState.DisabledVisible,
                            "MEUITM not imported to texture library");
                    }
                }
                else
                {
                    // does not have textures installed or textures installed but MEUITM not installed
                    if (meuitmFile.Ready)
                    {
                        options[InstallOption.MEUITM] = (OptionState.ForceCheckedVisible, null);
                    }
                    else
                    {
                        options[InstallOption.MEUITM] = (OptionState.DisabledVisible,
                            "MEUITM not imported to texture library, required for install in this mode");
                    }
                }

                // MEUITM mode user files
                if (filesForTarget.Any(x => x is UserFile && x.Ready))
                {
                    options[InstallOption.UserFiles] = (OptionState.CheckedVisible, null);
                }
                else
                {
                    options[InstallOption.UserFiles] = (OptionState.DisabledVisible,
                        "User files have been added for install but none of them are ready to install");
                }

                return options;
            }

            if (manifestMode == ManifestMode.ALOT)
            {
                // ALOT mode requires ALOT + ALOT update if applicable
                var alotFile = filesForTarget.FirstOrDefault(x =>
                    x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0);
                var alotUpdateFile = filesForTarget.FirstOrDefault(x =>
                    x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER > 0);


                // CHECK ALOT MAIN
                if (alotFile == null)
                {
                    Log.Error($"Manifest for ALOT mode is missing ALOT (game: {gameTarget.Game})!");
                    return options;
                }

                if (targetAlotInfo != null)
                {
                    // Textures have been installed
                    if (targetAlotInfo.ALOTVER > 0 && targetAlotInfo.ALOTVER != alotFile.AlotVersionInfo.ALOTVER)
                    {
                        // Not matching ALOT version
                        if (!alotFile.Ready)
                        {
                            options[InstallOption.ALOT] = (OptionState.DisabledVisible,
                                "ALOT installed, but not imported into texture library");
                        }
                        else
                        {
                            options[InstallOption.ALOT] = (OptionState.DisabledVisible,
                                $"{alotFile.FriendlyName} is not applicable to the current game installation");
                        }
                    }
                    else
                    {
                        if (alotFile.Ready)
                        {
                            options[InstallOption.ALOT] = (OptionState.UncheckedVisible, null);
                        }
                        else
                        {
                            options[InstallOption.ALOT] = (OptionState.DisabledVisible,
                                "ALOT installed, but not imported into texture library");
                        }
                    }
                }
                else
                {
                    // No textures have been installed
                    if (alotFile.Ready)
                    {
                        options[InstallOption.ALOT] = (OptionState.ForceCheckedVisible, null);
                    }
                    else
                    {
                        options[InstallOption.ALOT] = (OptionState.DisabledVisible, $"{alotFile.Filename} is required to be imported when installing in ALOT mode when ALOT is not already installed");
                    }
                }


                // CHECK ALOT UPDATE
                if (alotUpdateFile != null)
                {
                    if (targetAlotInfo != null)
                    {
                        // Textures have been installed
                        if (targetAlotInfo.ALOTVER > 0)
                        {
                            // ALOT installed. Check if update is applicable
                            if (targetAlotInfo.ALOTVER != alotUpdateFile.AlotVersionInfo.ALOTVER)
                            {
                                // Not matching ALOT version. Update is not applicable
                                if (alotUpdateFile.Ready)
                                {
                                    options[InstallOption.ALOTUpdate] = (OptionState.DisabledVisible, "ALOT installed, but not imported into texture library");
                                }
                                else
                                {
                                    options[InstallOption.ALOTUpdate] = (OptionState.DisabledVisible, $"{alotUpdateFile.FriendlyName} is not applicable to the current game installation");
                                }
                            }
                            else if (targetAlotInfo.ALOTUPDATEVER > alotUpdateFile.AlotVersionInfo.ALOTUPDATEVER)
                            {
                                // Downgrade, not alloweed
                                options[InstallOption.ALOTUpdate] = (OptionState.DisabledVisible, "Cannot downgrade installed installed, but not imported into texture library");
                            }
                            else if (targetAlotInfo.ALOTUPDATEVER < alotUpdateFile.AlotVersionInfo.ALOTUPDATEVER)
                            {
                                // This will update the install
                                options[InstallOption.ALOTUpdate] = (OptionState.ForceCheckedVisible, null);
                            }
                            else
                            {
                                // Same version as installed
                                options[InstallOption.ALOTUpdate] = (OptionState.UncheckedVisible, null);
                            }
                        }
                        else
                        {
                            // ALOT not installed but textures installed
                            if (alotFile.Ready)
                            {
                                // in ALOT mode in this case this update must be installed
                                options[InstallOption.ALOTUpdate] = (OptionState.ForceCheckedVisible, null);
                            }
                            else
                            {
                                options[InstallOption.ALOTUpdate] = (OptionState.DisabledVisible, "Textures installed but ALOT not installed, update not applicable");
                            }
                        }
                    }
                    else
                    {
                        // No textures have been installed
                        if (alotUpdateFile.Ready)
                        {
                            options[InstallOption.ALOTUpdate] = (OptionState.ForceCheckedVisible, null);
                        }
                        else
                        {
                            options[InstallOption.ALOTUpdate] = (OptionState.DisabledVisible, $"{alotUpdateFile.Filename} is required to be imported when installing in ALOT mode when ALOT is not already installed");
                        }
                    }
                }

                // CHECK ADDONS
                if (filesForTarget.Any(x =>
                    x is ManifestFile mf && mf.AlotVersionInfo.IsNotVersioned() || x is PreinstallMod))
                {
                    options[InstallOption.ALOTAddon] = (OptionState.CheckedVisible, null);
                }
                else
                {
                    options[InstallOption.ALOTAddon] = (OptionState.DisabledVisible, "No Addon files are imported");
                }

                // MEUITM
                if (gameTarget.Game == Enums.MEGame.ME1)
                {
                    var meuitmFile = filesForTarget.FirstOrDefault(x =>
                    x.AlotVersionInfo.MEUITMVER > 0);
                    if (meuitmFile == null)
                    {
                        // Not in manifest...?
                        Log.Error("ALOT manifest is missing MEUITM...?");
                    }
                    else
                    {

                        int meuitmFileVer = meuitmFile.AlotVersionInfo.MEUITMVER;
                        if (targetAlotInfo != null && targetAlotInfo.MEUITMVER > 0)
                        {
                            // Has some version of MEUITM installed
                            if (targetAlotInfo.MEUITMVER != meuitmFileVer)
                            {
                                options[InstallOption.MEUITM] = (OptionState.DisabledVisible,
                                    "Cannot install a different version of MEUITM on top of existing installed version");
                            }
                            else if (meuitmFile.Ready)
                            {
                                options[InstallOption.MEUITM] = (OptionState.UncheckedVisible, "MEUITM already installed");
                            }
                            else
                            {
                                options[InstallOption.MEUITM] = (OptionState.DisabledVisible,
                                    "MEUITM not imported to texture library");
                            }
                        }
                        else
                        {
                            // does not have textures installed or textures installed but MEUITM not installed
                            if (meuitmFile.Ready)
                            {
                                options[InstallOption.MEUITM] = (OptionState.CheckedVisible, null);
                            }
                            else
                            {
                                options[InstallOption.MEUITM] = (OptionState.DisabledVisible,
                                    "MEUITM not imported to texture library");
                            }
                        }
                    }
                }

                // CHECK USER FILES
                if (filesForTarget.Any(x => x is UserFile && x.Ready))
                {
                    options[InstallOption.UserFiles] = (OptionState.CheckedVisible, null);
                }
                else if (filesForTarget.Any(x => x is UserFile))
                {
                    options[InstallOption.UserFiles] = (OptionState.DisabledVisible,
                        "User files have been added for install but none of them are ready to install");
                }
                else
                {
                    options[InstallOption.UserFiles] = (OptionState.DisabledVisible, null);
                }
            }

            return options;
        }
    }
}
