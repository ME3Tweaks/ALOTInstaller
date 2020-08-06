using System.Collections.Generic;

namespace ALOTInstallerCore.Objects.Manifest
{
    public interface ConfigurableModInterface
    {
        string ChoiceTitle { get; set; }
        int SelectedIndex { get; set; }
        List<string> ChoicesHuman { get; }
    }
}
