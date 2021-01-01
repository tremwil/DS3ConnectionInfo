using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using System.Windows.Threading;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        const bool USE_TOPMOST = true;

        private WinAPI.RECT targetRect;
        private IntPtr targetHandle;
        private DispatcherTimer timer;
        private WindowInteropHelper interopHelper;
        public bool OverlayEnabled { get; private set; }

        public Point TextOffset { get; set; }
        public double Scale { get; set; }
        public bool ShowRegion { get; set; }

        public string overlayString = "";

        public OverlayWindow(Point offset, double scale, bool showRegion)
        {
            InitializeComponent();
            interopHelper = new WindowInteropHelper(this);
            TextOffset = new Point(0.025, 0.025);

            TextOffset = offset;
            Scale = scale;
            ShowRegion = showRegion;

            timer = new DispatcherTimer();
            timer.Tick += Update;
            timer.Interval = new TimeSpan(0, 0, 1); // 1 second
            timer.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            int exStyle = WinAPI.GetWindowLongPtr(interopHelper.Handle, -20).ToInt32();
            WinAPI.SetWindowLongPtr(interopHelper.Handle, -20, new IntPtr(exStyle | 0x80 | 0x20));

            targetHandle = WinAPI.FindWindow(null, "DARK SOULS III");
            if (USE_TOPMOST)
            {
                WinAPI.SetWindowZOrder(interopHelper.Handle, new IntPtr(-1), 0x010);
            }
            else
            {
                interopHelper.Owner = targetHandle;
                WinAPI.SetWindowZOrder(interopHelper.Handle, targetHandle, 0x210);
            }

        }

        private void Update(object sender, EventArgs e)
        {
            if (targetHandle == IntPtr.Zero)
                return;

            if (!WinAPI.IsWindow(targetHandle))
            {
                timer.Stop();
                Close();
                return;
            }

            WinAPI.GetWindowRect(targetHandle, out targetRect);

            Left = targetRect.x1;
            Top = targetRect.y1;
            Width = targetRect.x2 - targetRect.x1;
            Height = targetRect.y2 - targetRect.y1;

            Task.Run(() =>
            {
                string str = "DS3ConnectionInfo V3 - By tremwil";

                Player[] activePlayers = Player.ActivePlayers().ToArray();
                if (activePlayers.Length != 0)
                {
                    string[] fmtNames = activePlayers.Select(p => p.SteamName + 
                        ((p.CharName == "") ? "" : " (" + p.CharName + ")")
                    ).ToArray();

                    int colSz = fmtNames.Select(name => name.Length).Max();
                    for (int i = 0; i < activePlayers.Length; i++)
                    {
                        Player p = activePlayers[i];
                        str += string.Format("\n{0,-" + colSz.ToString() + "}  {1,-3}  ",
                            fmtNames[i], (p.Ip == null) ? "N/A" : p.Ping.ToString()
                        );
                        if (ShowRegion) str += (p.Ip == null) ? "[STEAM RELAY]" : p.Region;
                    }
                }
                Dispatcher.Invoke(() => overlayString = str);
            });

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext ctx)
        {
            base.OnRender(ctx);

            if (!USE_TOPMOST || WinAPI.GetForegroundWindow() == targetHandle)
            {
                FormattedText txt = new FormattedText(overlayString, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe"), Scale * Width / 128, Brushes.White, 1);

                Point origin = new Point((1 - TextOffset.X) * Width - txt.Width, TextOffset.Y * Height);
                Geometry outline = txt.BuildGeometry(origin);

                ctx.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 2), outline);
                ctx.DrawText(txt, origin);
            }
        }
    }
}
