using System.Collections.Generic;

namespace AlotAddOnGUI.classes
{
    public class ZipFile : ConfigurableModInterface
    {
        //  <zipfile optional="true" default="false" inarchivepath="MEUITM\mods\SoftShadowsauto.zip" gamepathdestination="Engine\Shaders" friendlyname="Soft Shadows"/>
        //Interface
        public string ChoiceTitle { get; set; }
        public List<string> ChoicesHuman { get { return new List<string>(Choices); } }
        public int SelectedIndex { get; set; }

        //Class Specific
        public bool Optional { get; set; }
        public bool DefaultOption { get; set; }
        public string InArchivePath { get; set; }
        public string GameDestinationPath { get; set; }
        public bool DeleteShaders { get; set; }
        public readonly string[] Choices = { "Install", "Don't install" };
        public int ID { get; set; }
        public bool MEUITMSoftShadows { get; internal set; }

        public bool IsSelectedForInstallation()
        {
            return SelectedIndex == 0;
        }
    }
}