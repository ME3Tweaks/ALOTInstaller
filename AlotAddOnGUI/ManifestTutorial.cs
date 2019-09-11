using System.Xml.Linq;

namespace AlotAddOnGUI
{
    internal class ManifestTutorial
    {
        public string Link { get; internal set; }
        public string Text { get; internal set; }
        public string ToolTip { get; internal set; }
        public bool MEUITMOnly { get; internal set; }
    }
}