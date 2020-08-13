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

        public ScrollDialog(string title, params Button[] buttons) : base(title, buttons)
        {

        }

        public static int Prompt(string title, string topmessage, string bottommessage, List<string> listItems, ColorScheme scheme, params string[] buttons)
        {
            int SCROLLVIEWER_WIDTH = 90;
            int response = -1;

            int topMessageHeight = TextFormatter.MaxLines(topmessage, SCROLLVIEWER_WIDTH);
            int bottomMessageHeight = TextFormatter.MaxLines(bottommessage, SCROLLVIEWER_WIDTH);

            int y = 0;
            //Buttons
            var buttonList = new List<Button>();
            foreach (var buttonText in buttons)
            {
                buttonList.Add(new Button(buttonText));
            }

            //Dialog


            ScrollDialog sd = new ScrollDialog(title, buttonList.ToArray())
            {
                Width = SCROLLVIEWER_WIDTH + 2,
                ColorScheme = scheme
            };
            sd.Add(new Label(topmessage)
            {
                X = 0,
                Y = y,
                Width = SCROLLVIEWER_WIDTH,
                Height = topMessageHeight
            });

            y += topMessageHeight;
            y++; // spacing for list
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
                    Width = Math.Max(maxW, SCROLLVIEWER_WIDTH)
                });
            }

            var scrollView = new ScrollView(new Rect(0, y, SCROLLVIEWER_WIDTH, 12))
            {
                ContentSize = new Size(maxW, maxH),
                KeepContentAlwaysInViewport = true,
                //ContentOffset = new Point (0, 0),
                //ShowVerticalScrollIndicator = true,
                //ShowHorizontalScrollIndicator = true,
                AutoHideScrollBars = true,
            };
            scrollView.Add(scrollableContent);
            sd.Add(scrollView);

            y += 12; //scrolview height
            y++; //space one out
            // Bottom message
            sd.Add(new Label(bottommessage)
            {
                X = 0,
                Y = y,
                Width = SCROLLVIEWER_WIDTH,
                Height = bottomMessageHeight
            });

            y += bottomMessageHeight;

            // Setup actions
            for (int n = 0; n < buttonList.Count; n++)
            {
                int buttonId = n;
                buttonList[n].Clicked += () =>
                {
                    response = buttonId;
                    Application.RequestStop();
                };
            }

            sd.Height = y + 4;

            Application.Run(sd);
            return response;
        }
    }
}
