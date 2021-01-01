using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using System.IO;

namespace DS3ConnectionInfo
{
    public static class Overlay
    {
        private static OverlayWindow window;
        private static Thread overlayThread;

        public static void Enable(JObject settings)
        {
            overlayThread = new Thread(() =>
            {
                window = new OverlayWindow(
                    new Point((double)settings["xOffset"], (double)settings["yOffset"]),
                    (double)settings["textScale"],
                    (bool)settings["showRegion"]
                );
                window.Closed += (s, e) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);

                window.Show();
                Dispatcher.Run();
            });
            overlayThread.SetApartmentState(ApartmentState.STA);
            overlayThread.Start();
        }

        public static void Disable()
        {
            if (window != null)
            {
                window.Dispatcher.InvokeAsync(window.Close);
            }
            window = null;
        }
    }
}
