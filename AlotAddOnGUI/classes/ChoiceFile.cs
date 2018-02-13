using System.Collections.Generic;

namespace AlotAddOnGUI.classes
{
    public class ChoiceFile
    {
        public string ChoiceTitle { get; set; }
        public List<PackageFile> Choices { get; set; }
        public int ID { get; set; }
    }
}