using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Defines a configurable set of installation items for a texture mod.
    /// </summary>
    public abstract class ConfigurableMod : INotifyPropertyChanged
    {
        /// <summary>
        /// The title of this configurable option
        /// </summary>
        public string ChoiceTitle { get; internal set; }
        /// <summary>
        /// The index of the chosen option
        /// </summary>
        public int SelectedIndex { get; set; }
        /// <summary>
        /// The list of visible choices to present to the user
        /// </summary>
        public virtual List<object> ChoicesHuman { get; internal set; }
        /// <summary>
        /// The default index that should be selected when user is prompted. If AllowNoInstall is true, there is an additional not-listed index that is cancel. It is always the last index.
        /// </summary>
        internal int DefaultSelectedIndex { get; set; }
        /// <summary>
        /// Specifies if this item allows option of not installing anything
        /// </summary>
        internal bool AllowNoInstall { get; set; }

        /// <summary>
        /// If object is optional it should be presented to the user. If it is not optional, it must be installed,
        /// and should not be presented to the user as an option. This is only here as both ZipFile and CopyFile support this feature
        /// </summary>
        public bool Optional { get; set; }

        internal bool IsSelectedForInstallation()
        {
            if (!AllowNoInstall) return true; //Forced install
            if (ChoicesHuman[SelectedIndex] is NullChoiceOption) return false;
            return true; //Something was selected
        }

        internal void AddNoInstallIfApplicable()
        {
            if (AllowNoInstall && !ChoicesHuman.Any(x => x is NullChoiceOption))
            {
                ChoicesHuman.Add(new NullChoiceOption());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Options for 'Don't install'
    /// </summary>
    public class NullChoiceOption
    {
        public override string ToString() => "Don't install";
    }
}
