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
            int SCROLLVIEWER_MAX_WIDTH = 90;
            int BELOW_MAXW_BUFFER = 5;
            int SCROLLBAR_WIDTH = 1;

            // Calculate sizes
            int maxW = listItems.Any() ? listItems.Max(x => x.Length) : 1; //widest string width

            int buttonsW = buttons.Sum(x => x.Length + 4);
            maxW = Math.Max(maxW, buttonsW);
            int tmaxW = Math.Max(maxW, topmessage.Length);
            tmaxW = Math.Max(maxW, bottommessage.Length);
            tmaxW = Math.Max(maxW, title.Length);
            int maxH = listItems.Count;
            int svHeight = Math.Min(maxH, 12);
            bool hasVerticalScrollbar = maxH > svHeight;
            int labelWidth = maxW;
            if (maxW < SCROLLVIEWER_MAX_WIDTH && hasVerticalScrollbar)
            {
                labelWidth = SCROLLVIEWER_MAX_WIDTH - SCROLLBAR_WIDTH;
            }

            if (tmaxW + BELOW_MAXW_BUFFER < SCROLLVIEWER_MAX_WIDTH)
            {
                SCROLLVIEWER_MAX_WIDTH = tmaxW + BELOW_MAXW_BUFFER;
            }


            int response = -1;

            int topMessageHeight = TextFormatter.MaxLines(topmessage, SCROLLVIEWER_MAX_WIDTH);
            int bottomMessageHeight = TextFormatter.MaxLines(bottommessage, SCROLLVIEWER_MAX_WIDTH);

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
                Width = SCROLLVIEWER_MAX_WIDTH + 2,
                ColorScheme = scheme
            };
            sd.Add(new Label(topmessage)
            {
                X = 0,
                Y = y,
                Width = SCROLLVIEWER_MAX_WIDTH,
                Height = topMessageHeight
            });

            y += topMessageHeight;
            y++; // spacing for list
                 //Build view


            View scrollableContent = new View()
            {
                Height = maxH,
                Width = labelWidth
            };

            scrollableContent.Add(new Label(string.Join("\n", listItems))
            {
                X = 0,
                Y = 0,
                Width = labelWidth
            });

            var scrollView = new ScrollView(new Rect(0, y, SCROLLVIEWER_MAX_WIDTH, svHeight))
            {
                ContentSize = new Size(maxW, maxH),
                //KeepContentAlwaysInViewport = true,
                //ContentOffset = new Point (0, 0),
                //ShowVerticalScrollIndicator = true,
                //ShowHorizontalScrollIndicator = true,
                AutoHideScrollBars = true,
            };
            scrollView.Add(scrollableContent);
            sd.Add(scrollView);

            y += svHeight; //scrolview height
            y++; //space one out
            // Bottom message
            sd.Add(new Label(bottommessage)
            {
                X = 0,
                Y = y,
                Width = SCROLLVIEWER_MAX_WIDTH,
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
