using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.IO;
using Steamworks;

namespace DS3ConnectionInfo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private DispatcherTimer gameStartTimer, updateTimer, pingFilterTimer;
        private ObservableCollection<Player> playerData;
        private OverlayWindow overlay;

        private int activeFilterEffect = 0;
        private int reoSpamCnt = 0;

        public MainWindow()
        {
            InitializeComponent();
            overlay = new OverlayWindow();
            Closed += MainWindow_Closed;

            gameStartTimer = new DispatcherTimer();
            gameStartTimer.Interval = TimeSpan.FromSeconds(1);
            gameStartTimer.Tick += GameStartTimer_Tick;
            gameStartTimer.Start();

            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromSeconds(0.5);
            updateTimer.Tick += UpdateTimer_Tick;

            pingFilterTimer = new DispatcherTimer();
            pingFilterTimer.Tick += PingFilterTimer_Tick;

            playerData = new ObservableCollection<Player>();
            dataGridSession.DataContext = playerData;
            overlay.dataGrid.DataContext = playerData;
            Title = "DS3 Connection Info " + VersionCheck.CurrentVersion;

            swColVisible.IsOn = Settings.Default.SessColumnVisibility[0] == "Visible";
            swOColVisible.IsOn = Settings.Default.OverlayColVisibility[0] == "Visible";
            textColDesc.Text = Settings.Default.SessColumnDescs[0];
            textOColDesc.Text = Settings.Default.SessColumnDescs[0];
            UpdateColVisibility();
            overlay.UpdateColVisibility();

            Task.Run(() =>
            {
                if (VersionCheck.FetchLatest())
                {
                    string v = VersionCheck.LatestRelease["tag_name"].ToString();
                    if (string.Compare(VersionCheck.CurrentVersion, v) < 0)
                    {
                        this.Invoke(() =>
                        {
                            linkUpdate.NavigateUri = new Uri("https://github.com/tremwil/DS3ConnectionInfo/releases/tag/" + v);
                            textUpdate.Text = string.Format("NEW VERSION ({0}), DOWNLOAD HERE", v);
                            this.ShowMessageAsync("New Version Available", string.Format("{0} is out! Click the link in the title bar to download it.", v));
                        });
                    }
                }
            });
        }

        private void UpdateColVisibility()
        {
            for (int i = 0; i < Settings.Default.SessColumnVisibility.Count; i++)
            {
                string vis = Settings.Default.SessColumnVisibility[i];
                dataGridSession.Columns[i].Visibility = (Visibility)Enum.Parse(typeof(Visibility), vis);
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            Player.UpdatePlayerList();
            Player.UpdateInGameInfo();
            playerData.Clear();

            foreach (Player p in Player.ActivePlayers())
                playerData.Add(p);

            // Queue position update after the overlay has re-rendered
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(overlay.UpdatePosition));

            if (Settings.Default.UsePingFilter)
            {
                DS3Interop.NetStatus status = DS3Interop.GetNetworkState();
                if (status == DS3Interop.NetStatus.Host || status == DS3Interop.NetStatus.TryCreateSession || DS3Interop.InLoadingScreen())
                {   // Someone invaded, or the local player summoned a phantom
                    pingFilterTimer.Stop();
                    activeFilterEffect = 0;
                }

                if (status == DS3Interop.NetStatus.None && activeFilterEffect == 11)
                {   // Basically resetting REO
                    if (reoSpamCnt >= 3)
                    {
                        DS3Interop.ApplyEffect(11);
                    }
                    reoSpamCnt = (reoSpamCnt + 1) % 5;
                }

                if (activeFilterEffect != 0 && status == DS3Interop.NetStatus.Client && !pingFilterTimer.IsEnabled)
                {   // Connection has been established
                    pingFilterTimer.Interval = TimeSpan.FromSeconds(Settings.Default.SamplingDelay);
                    pingFilterTimer.Start();
                }
            }
            else
            {
                activeFilterEffect = 0;
                pingFilterTimer.Stop();
            }
        }

        private void PingFilterTimer_Tick(object sender, EventArgs e)
        {
            double sumPing = 0;  int n = 0;
            foreach (Player p in Player.ActivePlayers())
            {
                if (p.Ping != -1)
                {
                    sumPing += p.Ping;
                    n++;
                }
            }
            if (n == 0)
            {   // Wait until one player has a ping
                pingFilterTimer.Interval = TimeSpan.FromSeconds(0.5);
                return;
            }
            if (sumPing / n > Settings.Default.MaxAvgPing)
            {
                DS3Interop.LeaveSession();
                if (activeFilterEffect != 11) DS3Interop.ApplyEffect(activeFilterEffect);
                else reoSpamCnt = 4;
            }
            else activeFilterEffect = 0;
            pingFilterTimer.Stop();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            overlay.Close();
            HotkeyManager.Disable();
            ETWPingMonitor.Stop();
            Settings.Default.Save();
        }

        private void GameStartTimer_Tick(object sender, EventArgs e)
        {   // Wait for both process attach & main window existing
            if (DS3Interop.TryAttach() && DS3Interop.FindWindow())
            {
                DS3Interop.Process.EnableRaisingEvents = true;
                DS3Interop.Process.Exited += DarkSouls_HasExited;
                labelGameState.Content = "DS3: RUNNING";
                labelGameState.Foreground = Brushes.LawnGreen;

                File.WriteAllText("steam_appid.txt", "374320");
                if (!SteamAPI.Init())
                {
                    MessageBox.Show("Could not initialize Steam API", "Steam API Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }

                if (!HotkeyManager.Enable())
                    MessageBox.Show("Could not initialize keyboard hook for hotkeys", "WINAPI Error", MessageBoxButton.OK, MessageBoxImage.Error);

                if (!overlay.InstallMsgHook())
                    MessageBox.Show("Could not overlay message hook", "WINAPI Error", MessageBoxButton.OK, MessageBoxImage.Error);

                overlay.UpdateVisibility();
                if (swBorderless.IsOn ^ DS3Interop.Borderless)
                    DS3Interop.MakeBorderless(swBorderless.IsOn);

                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.BorderlessHotkey, () => swBorderless.IsOn ^= true);
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.OverlayHotkey, () => swOverlay.IsOn ^= true);
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.PingFilterHotkey, () => Settings.Default.UsePingFilter ^= true);
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.REOHotkey, () => PingFilterAction(11));
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.RSDHotkey, () => PingFilterAction(10));
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.WSDHotkey, () => PingFilterAction(4));
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.LeaveSessionHotkey, () => DS3Interop.LeaveSession());

                ETWPingMonitor.Start();
                updateTimer.Start();
                gameStartTimer.Stop();
            }
        }

        private void PingFilterAction(int effect)
        {
            if (Settings.Default.UsePingFilter && !DS3Interop.InLoadingScreen())
            {
                pingFilterTimer.Stop();
                activeFilterEffect = (effect == activeFilterEffect) ? 0 : effect;
                if (activeFilterEffect != 11 || reoSpamCnt != 4) DS3Interop.ApplyEffect(effect);
                reoSpamCnt = 0;
            }
            else DS3Interop.ApplyEffect(effect);
        }

        private void DarkSouls_HasExited(object sender, EventArgs e)
        {
            updateTimer.Stop();
            SteamAPI.Shutdown();
            File.Delete("steam_appid.txt");
            Close();
        }

        private void btnFont_Click(object sender, RoutedEventArgs e)
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(System.Drawing.Font));

            var fD = new System.Windows.Forms.FontDialog();
            fD.Font = (System.Drawing.Font)converter.ConvertFromInvariantString(Settings.Default.OverlayFont);

            if (fD.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Default.OverlayFont = converter.ConvertToInvariantString(fD.Font);
            }
        }

        private void cbColName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            swColVisible.IsOn = Settings.Default.SessColumnVisibility[cbColName.SelectedIndex] == "Visible";
            textColDesc.Text = Settings.Default.SessColumnDescs[cbColName.SelectedIndex];
        }

        private void swColVisible_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            Settings.Default.SessColumnVisibility[cbColName.SelectedIndex] = swColVisible.IsOn ? "Visible" : "Hidden";
            UpdateColVisibility();
        }

        private void cbOColName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            swOColVisible.IsOn = Settings.Default.OverlayColVisibility[cbOColName.SelectedIndex] == "Visible";
            textOColDesc.Text = Settings.Default.SessColumnDescs[cbOColName.SelectedIndex];
        }

        private void swOColVisible_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            Settings.Default.OverlayColVisibility[cbOColName.SelectedIndex] = swOColVisible.IsOn ? "Visible" : "Hidden";
            overlay.UpdateColVisibility();
        }

        private void swOverlay_Toggled(object sender, RoutedEventArgs e)
        {
            if (IsInitialized) overlay.UpdateVisibility();
        }

        private void swBorderless_Toggled(object sender, RoutedEventArgs e)
        {
            if (DS3Interop.WinThread != 0 && swBorderless.IsOn ^ DS3Interop.Borderless)
                DS3Interop.MakeBorderless(swBorderless.IsOn);
        }

        private void btnResetSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.Reset();
            cbColName.SelectedIndex = 0;
            cbOColName.SelectedItem = 0;
            UpdateColVisibility();
        }

        private void webLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
