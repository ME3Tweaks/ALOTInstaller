using System.Collections.Generic;
using System.ComponentModel;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Object that allows extracting a zip archive into the game directory
    /// </summary>
    public class ZipFile : ConfigurableMod, INotifyPropertyChanged
    {
        //  <zipfile optional="true" default="false" inarchivepath="MEUITM\mods\SoftShadowsauto.zip" gamepathdestination="Engine\Shaders" friendlyname="Soft Shadows"/>
        
        public string InArchivePath { get; set; }
        public string GameDestinationPath { get; set; }
        public bool DeleteShaders { get; set; }
        public bool MEUITMSoftShadows { get; internal set; }
        public string StagedPath { get; set; }
        
    }
}