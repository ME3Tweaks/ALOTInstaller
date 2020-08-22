using System.Collections.Generic;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Object that allows copying single file into game directory
    /// </summary>
    public class CopyFile : ConfigurableMod
    {
        //  <copyfile optional="false" inarchivepath="MEUITM\mods\Splash.bmp" gamepathdestination="BioGame\Splash\Splash.bmp" friendlyname="MEUITM Splash Screen"/>
        public string StagedPath { get; set; }
        public string InArchivePath { get; set; }
        public string GameDestinationPath { get; set; }
    }
}