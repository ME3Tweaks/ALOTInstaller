using System.Collections.Generic;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Object that allows copying single file into game directory
    /// </summary>
    public class CopyFile : ConfigurableMod
    {
        //  <copyfile optional="false" inarchivepath="MEUITM\mods\Splash.bmp" gamepathdestination="BioGame\Splash\Splash.bmp" friendlyname="MEUITM Splash Screen"/>
        public CopyFile() : base()
        {
            ChoicesHuman = new List<object>();
            ChoicesHuman.Add("Install"); //Install is only option by default. Don't install will be auto added if this is optional.
        }
        public string StagedPath { get; set; }
        public string InArchivePath { get; set; }
        public string GameDestinationPath { get; set; }
    }
}