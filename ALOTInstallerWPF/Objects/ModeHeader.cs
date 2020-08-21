using System;
using System.Collections.Generic;
using System.Text;
using ALOTInstallerCore.Objects.Manifest;

namespace ALOTInstallerWPF.Objects
{
    public class ModeHeader
    {
        public ModeHeader(ManifestMode mode, string modeDirections, string modeDescription)
        {
            this.Mode = mode;
            this.ModeDirections = modeDirections;
            this.ModeDescription = modeDescription;
            ModeText = $"{mode} mode";
        }

        public string ModeDirections { get; set; }
        public ManifestMode Mode { get; set; }
        public string ModeText { get; set; }
        public string ModeDescription { get; set; }
    }
}
