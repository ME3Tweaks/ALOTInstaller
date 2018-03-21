using System.Collections.Generic;

namespace AlotAddOnGUI.classes
{
    public class CopyFile : ConfigurableModInterface
    {
        //  <copyfile optional="false" inarchivepath="MEUITM\mods\Splash.bmp" gamepathdestination="BioGame\Splash\Splash.bmp" friendlyname="MEUITM Splash Screen"/>
        //Interface
        public string ChoiceTitle { get; set; }
        public List<string> ChoicesHuman { get { return new List<string>(Choices); } }
        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get { return _selectedIndex != -1 ? _selectedIndex : (DefaultOption ? 0 : 1); }
            set { _selectedIndex = value; }
        }
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