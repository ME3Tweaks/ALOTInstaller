using System.ComponentModel;
using NStack;
using Terminal.Gui;

namespace ALOTInstallerConsole.UserControls
{
    public class ProgressDialog : Dialog, INotifyPropertyChanged
    {
        public long ProgressMax { get; set; } = 100;
        public long ProgressValue { get; set; }
        public string TopMessage { get; set; }
        public string BottomMessage { get; set; }

        private void OnProgressValueChanged()
        {
            if (ProgressMax == 0) return;
            progressBar.Fraction = ProgressValue * 1.0f / ProgressMax;
        }

        private void OnProgressMaxChanged()
        {
            if (ProgressMax == 0) return;
            progressBar.Fraction = ProgressValue * 1.0f / ProgressMax;
        }

        private void OnTopMessageChanged()
        {
            topMessageLabel.Text = TopMessage;
        }

        private void OnBottomMessageChanged()
        {
            bottomMessageLabel.Text = BottomMessage;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private ProgressBar progressBar;
        private Label topMessageLabel;
        private Label bottomMessageLabel;

        public ProgressDialog(ustring title, string topMessage, params Button[] buttons) : base(title, buttons)
        {
            topMessageLabel = new Label(topMessage)
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(),
                Height = 2,
            };
            Add(topMessageLabel);

            int progressBarPos = 4;
            bottomMessageLabel = new Label("Bottom message")
            {
                X = 1,
                Y = progressBarPos++,
                Width = Dim.Fill(),
                Height = 2,
            };
            Add(bottomMessageLabel);

            progressBar = new ProgressBar()
            {
                X = 1,
                Y = progressBarPos++,
                Width = 72,
                Height = 1,
                ColorScheme = Colors.Menu
            };
            Add(progressBar);

            Width = 75;
            Height = 9;
        }
    }
}
