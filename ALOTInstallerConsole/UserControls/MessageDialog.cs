using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace ALOTInstallerConsole.UserControls
{
    /// <summary>
    /// Textbox that will be displayed as a dialog with no buttons and must be manually killed. Useful for onscreen prompts
    /// </summary>
    public class MessageDialog : Dialog, INotifyPropertyChanged
    {
        private Label messageLabel;

        private readonly int maxwidth = 70;


        public MessageDialog(string initialMessage)
        {
            messageLabel = new Label("")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 3,
                TextAlignment = TextAlignment.Centered
            };
            Add(messageLabel);
            Message = initialMessage;

        }

        public void OnMessageChanged()
        {
            var uitext = getWrappedString(out var height);
            Height = height + 2;
            Width = uitext.Split(Environment.NewLine).Max(x => x.Length) + 8;
            messageLabel.Text = uitext;
        }

        private string getWrappedString(out int retNumLines)
        {
            int numberOfLines = 1;
            string[] words = Message.Split(' ');

            StringBuilder newSentence = new StringBuilder();


            string line = "";
            foreach (string word in words)
            {
                if ((line + word).Length > maxwidth)
                {
                    newSentence.AppendLine(line);
                    numberOfLines++;
                    line = "";
                }

                line += $"{word} ";
            }

            if (line.Length > 0)
                newSentence.AppendLine(line);
            retNumLines = numberOfLines;
            return newSentence.ToString();
        }

        public string Message { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
