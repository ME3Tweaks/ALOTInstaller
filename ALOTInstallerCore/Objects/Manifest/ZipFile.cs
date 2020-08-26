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
            InArchivePath = source.InArchivePath;
            GameDestinationPath = source.GameDestinationPath;
            DeleteShaders = source.DeleteShaders;
            MEUITMSoftShadows = source.MEUITMSoftShadows;
        }

        public string InArchivePath { get; set; }
        public string GameDestinationPath { get; set; }
        public bool DeleteShaders { get; set; }
        public bool MEUITMSoftShadows { get; internal set; }
        public string StagedPath { get; set; }

    }
}