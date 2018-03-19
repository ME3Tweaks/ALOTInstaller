using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    public interface ConfigurableModInterface
    {
        string ChoiceTitle { get; set; }
        int SelectedIndex { get; set; }
        List<string> ChoicesHuman { get; }
    }
}
