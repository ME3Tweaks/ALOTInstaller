using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Object that allows extracting a zip archive into the game directory
    /// </summary>
    public class ZipFile : ConfigurableMod, INotifyPropertyChanged
    {
        //  <zipfile optional="true" default="false" inarchivepath="MEUITM\mods\SoftShadowsauto.zip" gamepathdestination="Engine\Shaders" friendlyname="Soft Shadows"/>
        public ZipFile() : base()
        {
            ChoicesHuman = new List<object>();
            ChoicesHuman.Add("Install"); //Install is only option by default. Don't install will be auto added if this is optional.
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="source"></param>
        public ZipFile(ZipFile source) : base(source)
        {
            ChoicesHuman = source.ChoicesHuman.ToList(); //Duplicate the list instance so we don't hold same reference.
            SourceName = source.SourceName;
            GameDestinationPath = source.GameDestinationPath;
            DeleteShaders = source.DeleteShaders;
            MEUITMSoftShadows = source.MEUITMSoftShadows;
        }

        /// <summary>
        /// Where this file is located within the containing installer file archive
        /// </summary>
        public string SourceName { get; set; }
        /// <summary>
        /// Where the contents of this zip file will be extracted to relative to the game root
        /// </summary>
        public string GameDestinationPath { get; set; }
        /// <summary>
        /// If the local shaders cache should be deleted when this file is installed (only does something in ME1)
        /// </summary>
        public bool DeleteShaders { get; set; }
        /// <summary>
        /// If this is MEUITMSoftShadows or not
        /// </summary>
        public bool MEUITMSoftShadows { get; internal set; }
        /// <summary>
        /// Path where this archive file is staged at.
        /// </summary>
        public string StagedPath { get; set; }
    }
}