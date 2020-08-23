﻿using System.Collections.Generic;
using System.Linq;

namespace ALOTInstallerCore.Objects.Manifest
{
    public class ChoiceFile : ConfigurableMod
    {
        //Interface
        public override List<string> ChoicesHuman => Choices.Select(s => s.ChoiceTitle).ToList();

        //Class Specific
        public List<PackageFile> Choices { get; set; } // Support null option for none?
        public PackageFile GetChosenFile()
        {
            return Choices[SelectedIndex];
        }
    }
}