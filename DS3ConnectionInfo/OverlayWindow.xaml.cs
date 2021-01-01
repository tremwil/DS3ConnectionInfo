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

        public object lockObj = new object();

        private class Cell
        {
            public string text;
            public Brush color;
            public bool mcol;

            public FormattedText fmt;

            public Cell(string text)
            {
                this.text = text;
                this.mcol = false;
                this.color = Brushes.White;
            }

            public Cell(string text, bool mcol, Brush color)
            {
                this.text = text;
                this.mcol = mcol;
                this.color = color;
            }
        }

        private Cell[,] overlayCells;

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

            int pidDS3 = Process.GetProcessesByName("DarkSoulsIII")[0].Id;

            targetHandle = IntPtr.Zero;
            do
            {
                targetHandle = WinAPI.FindWindowEx(IntPtr.Zero, targetHandle, null, "DARK SOULS III");
                WinAPI.GetWindowThreadProcessId(targetHandle, out uint pid);
                if (pid == pidDS3) break;
            } while (targetHandle != IntPtr.Zero);

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
                Player[] activePlayers = Player.ActivePlayers().ToArray();
                Cell[,] newCells = new Cell[activePlayers.Length + 1, ShowRegion ? 3 : 2];
                newCells[0, 0] = new Cell("DS3ConnectionInfo V3.1 - by tremwil", true, Brushes.White);

                if (activePlayers.Length != 0)
                {
                    for (int i = 0; i < activePlayers.Length; i++)
                    {
                        Player p = activePlayers[i];
                        newCells[i + 1, 0] = new Cell(p.SteamName + ((p.CharName == "") ? "" : " (" + p.CharName + ")"));
                        newCells[i + 1, 1] = new Cell(p.Ping.ToString(), false, pingColor(p.Ping));

                        if (ShowRegion) 
                           newCells[i + 1, 2] = new Cell((p.Ip == null) ? "[STEAM RELAY]" : p.Region);
                    }
                }
                Dispatcher.Invoke(() => overlayCells = newCells);
            });

            InvalidateVisual();
        }

        private Brush pingColor(int ping)
        {
            switch (ping)
            {
                case -1:
                    return Brushes.White;
                case int n when (n <= 50):
                    return Brushes.Blue;
                case int n when (n <= 100):
                    return Brushes.Green;
                case int n when (n <= 200):
                    return Brushes.Yellow;
                default:
                    return Brushes.Red;
            }
        }

        protected override void OnRender(DrawingContext ctx)
        {
            base.OnRender(ctx);

            if (!USE_TOPMOST || WinAPI.GetForegroundWindow() == targetHandle && overlayCells != null)
            {
                lock (lockObj)
                {
                    var padText = new FormattedText("AA", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe"), Scale * Width / 128, Brushes.White, 1);
                    double padX = padText.WidthIncludingTrailingWhitespace;
                    double padY = padText.Height;

                    foreach (Cell c in overlayCells)
                        if (c != null) 
                            c.fmt = new FormattedText(c.text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe"), Scale * Width / 128, c.color, 1);

                    double[] colWidths = new double[overlayCells.GetLength(1)];
                    for (int col = 0; col < overlayCells.GetLength(1); col++)
                    {
                        colWidths[col] = Enumerable.Range(0, overlayCells.GetLength(0)).Select(row =>
                            (overlayCells[row,col] == null || overlayCells[row,col].mcol) ? 0 : overlayCells[row, col].fmt.Width
                        ).Max();
                    }
                    double mcolMax = Enumerable.Range(0, overlayCells.GetLength(0)).Select(row =>
                        (overlayCells[row, 0] == null || !overlayCells[row, 0].mcol) ? 0 : overlayCells[row, 0].fmt.Width
                    ).Max();

                    double totalWidth = Math.Max((overlayCells.GetLength(1) - 1) * padX + colWidths.Sum(), mcolMax);
                    
                    Pen outlinePen = new Pen(Brushes.Black, 2);
                    Vector offset = new Vector();
                    Point origin = new Point((1 - TextOffset.X) * Width - totalWidth, TextOffset.Y * Height);

                    for (int row = 0; row < overlayCells.GetLength(0); row++)
                    {
                        offset = new Vector(0, row * padY); 
                        for (int col = 0; col < overlayCells.GetLength(1); col++)
                        {
                            Cell c = overlayCells[row, col];
                            if (c != null)
                            {
                                Geometry outline = c.fmt.BuildGeometry(origin + offset);
                                ctx.DrawGeometry(c.color, outlinePen, outline);
                                ctx.DrawText(c.fmt, origin + offset);
                            }
                            offset.X += colWidths[col] + padX;
                        }
                    }
                }
            }
        }
    }
}
