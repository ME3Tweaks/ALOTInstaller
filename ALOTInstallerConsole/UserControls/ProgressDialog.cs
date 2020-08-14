using System.ComponentModel;
using System.Threading;
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
            Application.MainLoop.Invoke(() => progressBar.Fraction = ProgressValue * 1.0f / ProgressMax);
        }

        private void OnProgressMaxChanged()
        {
            if (ProgressMax == 0) return;
            Application.MainLoop.Invoke(() => progressBar.Fraction = ProgressValue * 1.0f / ProgressMax);
        }

        private void OnTopMessageChanged()
        {
            Application.MainLoop.Invoke(() => topMessageLabel.Text = TopMessage);
        }

        private void OnBottomMessageChanged()
        {
            Application.MainLoop.Invoke(() => bottomMessageLabel.Text = BottomMessage);
        }

        public bool Indeterminate { get; set; }

        private Timer currentIndeterminateTimer;
        private void OnIndeterminateChanged()
        {
            if (Indeterminate)
            {
                currentIndeterminateTimer = new Timer((x) =>
                {
                    Application.MainLoop.Invoke(()=> progressBar.Pulse());
                }, null, 500, 200);
            }
            else
            {
                if (currentIndeterminateTimer != null)
                {
                    currentIndeterminateTimer.Dispose();
                    currentIndeterminateTimer = null;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private ProgressBar progressBar;
        private Label topMessageLabel;
        private Label bottomMessageLabel;

        public ProgressDialog(ustring title, string topMessage, string initialBottomMessage, bool showProgressBar, params Button[] buttons) : base(title, buttons)
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
            bottomMessageLabel = new Label(initialBottomMessage)
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
            if (showProgressBar)
            {
                Add(progressBar);
            }

            Width = 75;
            Height = 9;
        }
    }
}
