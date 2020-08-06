using System.Collections.Generic;
using System.Linq;

namespace ALOTInstallerCore.Objects.Manifest
{
    public class ChoiceFile : ConfigurableModInterface
    {
        //Interface
        public string ChoiceTitle { get; set; }
        public List<string> ChoicesHuman { get => Choices.Select(s => s.ChoiceTitle).ToList(); }
        public int SelectedIndex { get; set; }

        //Class Specific
        public List<PackageFile> Choices { get; set; } // Support null option for none?
        public int ID { get; set; }
        public PackageFile GetChosenFile()
        {
            return Choices[SelectedIndex];
        }
    }
}