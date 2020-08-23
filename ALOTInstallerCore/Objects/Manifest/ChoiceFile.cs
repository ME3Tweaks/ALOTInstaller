using System.Collections.Generic;
using System.Linq;

namespace ALOTInstallerCore.Objects.Manifest
{
    public class ChoiceFile : ConfigurableMod
    {
        public override List<object> ChoicesHuman => Choices.Select(s => (object)s.ChoiceTitle).ToList();

        //Class Specific
        public List<PackageFile> Choices { get; set; } // Support null option for none?
        public PackageFile GetChosenFile()
        {
            var uiChoice = ChoicesHuman[SelectedIndex];
            if (!(uiChoice is NullChoiceOption))
            {
                return Choices[SelectedIndex];
            }

            return null; // No choice was taken
        }
    }
}