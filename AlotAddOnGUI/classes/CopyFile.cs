using System.Collections.Generic;

namespace AlotAddOnGUI.classes
{
    public class CopyFile : ConfigurableModInterface
    {
        //  <copyfile optional="false" inarchivepath="MEUITM\mods\Splash.bmp" gamepathdestination="BioGame\Splash\Splash.bmp" friendlyname="MEUITM Splash Screen"/>
        //Interface
        public string ChoiceTitle { get; set; }
        public List<string> ChoicesHuman { get { return new List<string>(Choices); } }
        public int SelectedIndex { get; set; }

        //Class Specific
        public bool Optional { get; set; }
        public bool DefaultOption { get; set; }
        public string InArchivePath { get; set; }
        public string GameDestinationPath { get; set; }
        public string FriendlyName { get; set; }
        public readonly string[] Choices = { "Install", "Don't install" };
        public int ID { get; set; }
        public bool IsSelectedForInstallation()
        {
            return SelectedIndex == 0;
        }
    }
}