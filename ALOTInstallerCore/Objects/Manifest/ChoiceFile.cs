using System.Collections.Generic;
using System.Linq;

namespace ALOTInstallerCore.Objects.Manifest
{
    public class ChoiceFile : ConfigurableMod
    {
        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="source"></param>
        public ChoiceFile(ChoiceFile source) : base(source)
        {
            ChoicesHuman = source.ChoicesHuman.OfType<PackageFile>().Select(x => new PackageFile(x)).OfType<object>().ToList();
        }

        public ChoiceFile()
        {
            ChoicesHuman = new List<object>();
        }

        //Class Specific
        public PackageFile GetChosenFile()
        {
            var uiChoice = ChoicesHuman[SelectedIndex];
            if (!(uiChoice is NullChoiceOption))
            {
                return ChoicesHuman[SelectedIndex] as PackageFile;
            }

            return null; // No choice was taken
        }
    }
}