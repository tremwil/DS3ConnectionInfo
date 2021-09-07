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
using System.Security.Permissions;
using System.Media;

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
        //private StreamWriter logWriter;

        private bool reoSpamming = false;
        private int reoSpamCnt = 0;
        private bool pingCheked = false;
        private bool hadInvaded = false;

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowUnhandledException((Exception)e.ExceptionObject, "CurrentDomain", e.IsTerminating);
            TaskScheduler.UnobservedTaskException += (s, e) => ShowUnhandledException(e.Exception, "TaskScheduler", false);
            Dispatcher.UnhandledException += (s, e) => { if (!Debugger.IsAttached) ShowUnhandledException(e.Exception, "Dispatcher", true); };

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

        private void ShowUnhandledException(Exception err, string type, bool fatal)
        {
            MetroDialogSettings diagSettings = new MetroDialogSettings()
            {
                ColorScheme = MetroDialogColorScheme.Accented,
                AffirmativeButtonText = "Copy",
                NegativeButtonText = "Close"
            };

            SystemSounds.Exclamation.Play();
            var result = this.ShowModalMessageExternal($"Unhandled Exception: {err.GetType().Name}", $"{err.Message}\n{err.StackTrace}", MessageDialogStyle.AffirmativeAndNegative, diagSettings);
            if (result == MessageDialogResult.Affirmative)
                Clipboard.SetText($"{err.GetType().Name}: {err.Message}\n{err.StackTrace}");

            Close();
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

            foreach (Player p in Player.ActivePlayers().OrderBy(p => p.TeamAlliegance))
                playerData.Add(p);

            // Update session info column sizes
            foreach (var col in dataGridSession.Columns)
            {
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Pixel);
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }
            dataGridSession.UpdateLayout();

            // Update overlay column sizes
            foreach (var col in overlay.dataGrid.Columns)
            {
                col.Width = new DataGridLength(1, DataGridLengthUnitType.Pixel);
                col.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            }
            overlay.dataGrid.UpdateLayout();

            // Queue position update after the overlay has re-rendered
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(overlay.UpdatePosition));

            DS3Interop.NetStatus status = DS3Interop.GetNetworkState();
            if (reoSpamming && !Settings.Default.SpamRedEyeOrb)
            {
                reoSpamming = false;
                reoSpamCnt = 0;
            }
            if (status == DS3Interop.NetStatus.None)
            {
                if (hadInvaded && !reoSpamming) DS3Interop.ApplyEffect(11);
                if (reoSpamming)
                {
                    reoSpamCnt = (reoSpamCnt + 1) % 5;
                    if (DS3Interop.IsSearchingInvasion() ^ (reoSpamCnt != 0)) DS3Interop.ApplyEffect(11);
                }
                hadInvaded = false;
                pingCheked = false;
            }
            if (Settings.Default.UsePingFilter)
            {
                if (status == DS3Interop.NetStatus.Host || status == DS3Interop.NetStatus.TryCreateSession || DS3Interop.InLoadingScreen())
                {   // Someone invaded, or the local player summoned a phantom, or ping filter was too late
                    pingFilterTimer.Stop();
                    reoSpamming = false;
                    reoSpamCnt = 0;
                    pingCheked = false;
                }
                if (status == DS3Interop.NetStatus.Client && !pingFilterTimer.IsEnabled && !pingCheked)
                {   // Connection has been established
                    pingCheked = true;
                    pingFilterTimer.Interval = TimeSpan.FromSeconds(Settings.Default.SamplingDelay);
                    pingFilterTimer.Start();
                }
            }
            else pingFilterTimer.Stop();
        }

        private void PingFilterTimer_Tick(object sender, EventArgs e)
        {
            double sumPing = 0;  int n = 0;
            bool absPingRespected = true;
            foreach (Player p in Player.ActivePlayers())
            {
                if (p.Ping != -1)
                {
                    absPingRespected &= p.Ping < Settings.Default.MaxAbsPing;
                    sumPing += p.Ping;
                    n++;
                }
            }
            if (n == 0)
            {   // Wait until at least one player has a ping
                pingFilterTimer.Interval = TimeSpan.FromSeconds(0.5);
                return;
            }
            if ((!absPingRespected || sumPing / n > Settings.Default.MaxAvgPing) && !DS3Interop.InLoadingScreen())
            {
                var joinMethod = DS3Interop.GetJoinMethod();
                DS3Interop.LeaveSession();
                if (joinMethod == DS3Interop.JoinMethod.RedEyeOrb)
                    hadInvaded = true;
                else if (joinMethod == DS3Interop.JoinMethod.RedSign)
                    DS3Interop.ApplyEffect(10);
                else if (joinMethod == DS3Interop.JoinMethod.WhiteSign)
                    DS3Interop.ApplyEffect(4);
            }
            else reoSpamming = false;
            pingFilterTimer.Stop();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            Settings.Default.Save();
            overlay.Close();
            HotkeyManager.Disable();
            ETWPingMonitor.Stop();
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

                if (Settings.Default.UseHotkeys && !HotkeyManager.Enable())
                    MessageBox.Show("Could not initialize keyboard hook for hotkeys", "WINAPI Error", MessageBoxButton.OK, MessageBoxImage.Error);

                if (!overlay.InstallMsgHook())
                    MessageBox.Show("Could not setup overlay message hook", "WINAPI Error", MessageBoxButton.OK, MessageBoxImage.Error);

                overlay.UpdateVisibility();
                if (swBorderless.IsOn ^ DS3Interop.Borderless)
                    DS3Interop.MakeBorderless(swBorderless.IsOn);

                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.BorderlessHotkey, () => swBorderless.IsOn ^= true);
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.OverlayHotkey, () => swOverlay.IsOn ^= true);
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.PingFilterHotkey, () => Settings.Default.UsePingFilter ^= true);
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.REOHotkey, () => OnlineHotkey(11));
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.RSDHotkey, () => OnlineHotkey(10));
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.WSDHotkey, () => OnlineHotkey(4));
                HotkeyManager.AddHotkey(DS3Interop.WinHandle, () => Settings.Default.LeaveSessionHotkey, () => DS3Interop.LeaveSession());

                ETWPingMonitor.Start();
                updateTimer.Start();
                gameStartTimer.Stop();
            }
        }

        private void OnlineHotkey(int effect)
        {
            if (DS3Interop.InLoadingScreen()) return;
            if (effect == 11 && Settings.Default.SpamRedEyeOrb)
            {
                reoSpamming ^= true;
                reoSpamCnt = 0;
                if (!reoSpamming && DS3Interop.IsSearchingInvasion())
                    DS3Interop.ApplyEffect(11);
            }
            else DS3Interop.ApplyEffect(effect);
        }

        private void DarkSouls_HasExited(object sender, EventArgs e)
        {
            this.Invoke(() =>
            {
                updateTimer.Stop();
                SteamAPI.Shutdown();
                Close();
            });
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

        private void swToggleHotkeys_Toggled(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.UseHotkeys && !HotkeyManager.Enable())
                MessageBox.Show("Could not initialize keyboard hook for hotkeys", "WINAPI Error", MessageBoxButton.OK, MessageBoxImage.Error);

            if (!Settings.Default.UseHotkeys) HotkeyManager.Disable();
        }

        private void headerFmt_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings");
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
