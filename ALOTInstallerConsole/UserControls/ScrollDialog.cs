using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerCore.Helpers;
using Terminal.Gui;

namespace ALOTInstallerConsole.UserControls
{
    /// <summary>
    /// Dialog with scrollable content (for possibly long lists of things)
    /// </summary>
    public class ScrollDialog : Dialog
    {
        public ScrollDialog(string title, string topMessage, string bottommessage, View scrollableContent, int cWidth, int cHeight, params Button[] buttons) : base(title, buttons)
        {
            Height = 20;
            Width = 50;

            Add(new Label(topMessage)
            {
                X = 0,
                Y = 0,
                //Width = 15,
                Height = 2
            });

            var scrollView = new ScrollView(new Rect(1, 3, 47, 12))
            {
                Y = 2,
                ContentSize = new Size(cWidth, cHeight),
                //ContentOffset = new Point (0, 0),
                //ShowVerticalScrollIndicator = true,
                //ShowHorizontalScrollIndicator = true,
                AutoHideScrollBars = true,
                ColorScheme = Colors.TopLevel,

            };
            scrollView.Add(scrollableContent);

            Add(scrollView);

            //Add(new Label(bottommessage)
            //{
            //    X = 0,
            //    Y = Pos.Bottom(this) - 5,
            //    Width = Dim.Fill(),
            //    Height = 1
            //});
        }

        public ScrollDialog(string title) : base(title)
        {

        }

        public static int Prompt(string title, string message, List<string> listItems, params string[] buttons)
        {
            int SCROLLVIEWER_WIDTH = 50;
            int response = -1;
            ScrollDialog sd = new ScrollDialog(title)
            {
                Width = SCROLLVIEWER_WIDTH + 2,
                Height = 20
            };
            int y = 0;
            sd.Add(new Label(message)
            {
                X = 0,
                Y = y,
                Height = 2
            });

            //Build view
            int maxW = listItems.Max(x => x.Length); //widest string width
            int maxH = listItems.Count;
            View scrollableContent = new View()
            {
                Height = maxH,
                Width = maxW
            };

            for (int i = 0; i < listItems.Count; i++)
            {
                var str = listItems[i];
                scrollableContent.Add(new Label(str)
                {
                    X = 0,
                    Y = i,
                    Width = Math.Max(maxW, SCROLLVIEWER_WIDTH),
                    ColorScheme = Colors.Menu
                });
            }

            var scrollView = new ScrollView(new Rect(0, 2, SCROLLVIEWER_WIDTH, 12))
            {
                Y = 1,
                ContentSize = new Size(maxW, maxH),
                //ContentOffset = new Point (0, 0),
                //ShowVerticalScrollIndicator = true,
                //ShowHorizontalScrollIndicator = true,
                AutoHideScrollBars = true,
                ColorScheme = Colors.TopLevel,
            };
            scrollView.Add(scrollableContent);
            sd.Add(scrollView);

            //Buttons
            foreach(var )

            Application.Run(sd);

            return response;
        }
    }
}
