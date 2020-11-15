using System;
using System.Collections.Generic;
using System.Text;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// MEUITM mode settings class. These are parsed off the MEUITM Manifest File and are read by installer to configure background and music.
    /// </summary>
    public class MEUITMModeSettings
    {
        public string MEUITMModeBackgroundPath { get; set; }

        public string MEUITMModeMusicPath { get; set; }
#if WPF
        public byte[] BackgroundImageBytes { get; set; } //We don't set it here cause we don't have access to image stuff in Core

        // there are no music bytes as without a library WPF app will not support streaming audio.
#endif
    }
}
