using DS3ConnectionInfo.WinAPI;
using System;
using System.Windows;
using System.Windows.Interop;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private WindowInteropHelper interopHelper;
        private User32.WinEventDelegate locEvtHook, focusEvtHook;
        private IntPtr hLocHook, hFocusHook;

        public OverlayWindow()
        {
            InitializeComponent();

            interopHelper = new WindowInteropHelper(this);
            int exStyle = User32.GetWindowLongPtr(interopHelper.Handle, -20).ToInt32();
            User32.SetWindowLongPtr(interopHelper.Handle, -20, new IntPtr(exStyle | 0x80 | 0x20));
            User32.SetWindowZOrder(interopHelper.Handle, new IntPtr(-1), 0x010);
            UpdateColVisibility();
        }

        public void UpdateColVisibility()
        {
            for (int i = 0; i < Settings.Default.OverlayColVisibility.Count; i++)
            {
                string vis = Settings.Default.OverlayColVisibility[i];
                dataGrid.Columns[i].Visibility = (Visibility)Enum.Parse(typeof(Visibility), vis);
            }
        }
        public void UpdateVisibility()
        {
            bool shouldShow = DS3Interop.IsGameFocused() || IsActive;
            if (!IsVisible && Settings.Default.DisplayOverlay && shouldShow)
            {
                Show();
                UpdatePosition();
            }
            if (IsVisible && !(Settings.Default.DisplayOverlay && shouldShow)) Hide();
        }

        public bool InstallMsgHook()
        {
            locEvtHook = new User32.WinEventDelegate(LocChangeHook);
            focusEvtHook = new User32.WinEventDelegate(FocusChangeHook);
            hLocHook = User32.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, locEvtHook, (uint)DS3Interop.Process.Id, DS3Interop.WinThread, 3);
            hFocusHook = User32.SetWinEventHook(0x8005, 0x8005, IntPtr.Zero, focusEvtHook, 0, 0, 0);
            return hLocHook != IntPtr.Zero && hFocusHook != IntPtr.Zero;
        }

        private void LocChangeHook(IntPtr hWinEventHook, uint eventType, IntPtr lParam, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == 0x800b && lParam == DS3Interop.WinHandle && idObject == 0)
                UpdatePosition();
        }

        private void FocusChangeHook(IntPtr hWinEventHook, uint eventType, IntPtr lParam, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == 0x8005 && idChild == 0)
                UpdateVisibility();
        }

        public void UpdatePosition()
        {
            // Handle Windows DPI scaling (thanks anmer)
            var psc = PresentationSource.FromVisual(this);
            if (psc != null)
            {
                double dpi = psc.CompositionTarget.TransformToDevice.M11;
                User32.GetWindowRect(DS3Interop.WinHandle, out RECT targetRect);

                Rect wpfRect = new Rect(targetRect.x1 / dpi, targetRect.y1 / dpi,
                    (targetRect.x2 - targetRect.x1) / dpi, (targetRect.y2 - targetRect.y1) / dpi);

                Left = wpfRect.Left + ((Settings.Default.OverlayAnchor % 2 == 1) ? Settings.Default.XOffset * wpfRect.Width :
                    (1 - Settings.Default.XOffset) * wpfRect.Width - ActualWidth);
                Top = wpfRect.Top + ((Settings.Default.OverlayAnchor < 2) ? Settings.Default.YOffset * wpfRect.Height :
                    (1 - Settings.Default.YOffset) * wpfRect.Height - ActualHeight);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            User32.UnhookWinEvent(hLocHook);
            User32.UnhookWinEvent(hFocusHook);
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            UpdatePosition();
        }
    }
}
