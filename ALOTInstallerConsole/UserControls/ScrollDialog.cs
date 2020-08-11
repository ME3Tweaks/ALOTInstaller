using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;

namespace ALOTInstallerConsole.UserControls
{
    /// <summary>
    /// Dialog with scrollable content (for possibly long lists of things)
    /// </summary>
public class ScrollDialog : Dialog
{
    public ScrollDialog(string title, string topMessage, string bottommessage, View scrollableContent, params Button[] buttons) : base(title, buttons)
    {
        Height = 20;
        Width = 50;

        Add(new Label(topMessage)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 2
        });

        var scrollView = new ScrollView()
        {
            Y = 2,
            ContentSize = new Size(28, 10),
            //ContentOffset = new Point (0, 0),
            ShowVerticalScrollIndicator = true,
            ShowHorizontalScrollIndicator = true,
            //AutoHideScrollBars = true,
            ColorScheme = Colors.TopLevel,
            Width = 50,
            Height = 12,
        };
        scrollView.Add(scrollableContent);

        Add(scrollView);

        Add(new Label(bottommessage)
        {
            X = 0,
            Y = Pos.Bottom(this) - 5,
            Width = Dim.Fill(),
            Height = 1
        });
    }
}
}
