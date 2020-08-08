using Terminal.Gui;

namespace ALOTInstallerConsole
{
    /// <summary>
    /// Class to help handle the gui.cs framework
    /// </summary>
    public static class UITools
    {
        /// <summary>
        /// Set's the text on a text field. This method can bypass the current restriction's of ReadOnly blocking setting text programatically
        /// </summary>
        /// <param name="tf"></param>
        /// <param name="text"></param>
        public static void SetText(TextField tf, string text)
        {
            var readOnly = tf.ReadOnly;
            tf.ReadOnly = false;
            tf.Text = text;
            tf.ReadOnly = readOnly;
        }
    }
}
