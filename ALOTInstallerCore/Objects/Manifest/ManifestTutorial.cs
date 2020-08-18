namespace ALOTInstallerCore.Objects.Manifest
{
    /// <summary>
    /// Class that is used for setting up info to display a tutorial link/button to a user
    /// </summary>
    public class ManifestTutorial
    {
        public string Link { get; internal set; }
        public string Text { get; internal set; }
        public string ToolTip { get; internal set; }
    }
}
