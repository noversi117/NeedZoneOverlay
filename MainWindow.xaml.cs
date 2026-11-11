using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace NeedZones
{
    public partial class MainWindow : Window
    {
        public static string AppDir => AppContext.BaseDirectory;
        static string DataDir => Path.Combine(AppDir, "data");
        static string IconPath => Path.Combine(AppDir, "icon.ico");

        readonly Settings s;
        readonly GameClock clock;
        readonly Dictionary<string, Reserve> reserves;
        readonly List<string> loadErrors;
        readonly ObservableCollection<RowVM> rows = new ObservableCollection<RowVM>();
        readonly DispatcherTimer tick;
        bool interactive;
        bool updatingPopup;
        bool exiting;
        DateTime popupClosedAt = DateTime.MinValue;
        IntPtr hwnd;
        HwndSource hwndSource;
        System.Windows.Forms.NotifyIcon tray;
        SyncWindow syncWindow;
        Updater.Release pendingUpdate;
        bool updating;

        const int HkVisible = 1, HkInteract = 2, HkSync = 3;

        public MainWindow()
        {
            InitializeComponent();
            s = Settings.Load();
            clock = new GameClock(s);
            (reserves, loadErrors) = Schedule.LoadReserves(DataDir);
            interactive = s.StartInteractive;

            ApplyTheme();
            RowsList.ItemsSource = rows;
            SettingsPopup.CustomPopupPlacementCallback = PlacePopupUnderRightEdge;
            // If the saved position is off-screen (e.g. a monitor was unplugged), start top-left instead
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            if (s.X < vl - 50 || s.X > vl + vw - 120 || s.Y < vt - 5 || s.Y > vt + vh - 60)
            {
                s.X = 40;
                s.Y = 120;
            }
            Left = s.X;
            Top = s.Y;
            s.Alpha = Math.Clamp(s.Alpha, 0.3, 1.0);
            Opacity = s.Alpha;
            SyncButton.ToolTip = $"Enter the in-game time ({Native.NiceHotkey(s.HotkeySyncClock)})";
            UpdateMinIcon();
            SyncSelectedOnlyChecks();
            RenderRows();
            ApplySize();
            UpdateFooter();

            Updater.CleanupOldFiles();
            Loaded += async (_, __) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(4));
                await CheckForUpdateAsync(silent: true);
            };

            tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            tick.Tick += (_, __) => UpdateValues();
            tick.Start();

            SourceInitialized += OnSourceInitialized;
            Closing += (_, e) =>
            {
                if (exiting) return;
                e.Cancel = true;
                ExitApp();
            };
        }

        // ---------- theme & size ----------

        Theme T => Theme.Get(s.Theme);

        void ApplyTheme()
        {
            T.Apply(Application.Current.Resources, s.TextSize);
            UpdateValues();
            Dispatcher.BeginInvoke(new Action(CenterTitleCaps), DispatcherPriority.Loaded);
        }

        // All-caps text in a condensed font sits off-center because the font reserves uneven space
        // above and below the letters. Measure the font and shift the title so the capitals are centered.
        void CenterTitleCaps()
        {
            var tb = TitleText;
            double size = tb.FontSize;
            if (string.IsNullOrEmpty(tb.Text) || double.IsNaN(size) || size <= 0) return;
            var tf = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
            double caps = tf.TryGetGlyphTypeface(out var gt) ? gt.CapsHeight * size : 0.7 * size;
            var ft = new FormattedText(tb.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, size,
                                       Brushes.Black, VisualTreeHelper.GetDpi(tb).PixelsPerDip);
            double offset = ft.Height / 2 - (ft.Baseline - caps / 2);   // + moves the text down
            tb.RenderTransform = new TranslateTransform(0, Math.Round(offset * 2) / 2);
        }

        double DefaultWidth => Math.Round(400.0 * s.TextSize / 13.0);

        void ApplySize()
        {
            if (s.Minimized)   // compact: fit the selected species, keep the chosen width
            {
                MaxHeight = 720;
                Width = s.W ?? DefaultWidth;
                SizeToContent = SizeToContent.Height;
                return;
            }
            if (s.W.HasValue && s.H.HasValue)
            {
                SizeToContent = SizeToContent.Manual;
                MaxHeight = SystemParameters.WorkArea.Height;
                Width = s.W.Value;
                Height = s.H.Value;
            }
            else
            {
                MaxHeight = 720;
                Width = DefaultWidth;
                SizeToContent = SizeToContent.Height;
            }
        }

        void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (SizeToContent != SizeToContent.Manual)
            {
                double h = ActualHeight;
                SizeToContent = SizeToContent.Manual;
                MaxHeight = SystemParameters.WorkArea.Height;
                Height = h;
            }
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
            Height = Math.Max(160, Height + e.VerticalChange);
        }

        void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            s.W = Width;
            s.H = Height;
            s.Save();
        }

        void ResizeGrip_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            s.W = null;
            s.H = null;
            s.Save();
            ApplySize();
        }

        // Compact mode: keep the title bar and show only the selected species with their times.
        void MinButton_Click(object sender, RoutedEventArgs e)
        {
            s.Minimized = !s.Minimized;
            s.Save();
            UpdateMinIcon();
            RenderRows();
            ApplySize();
        }

        void UpdateMinIcon()
        {
            MinIcon.Data = Geometry.Parse(s.Minimized ? "M4,6 L8,10 L12,6" : "M4,10 L8,6 L12,10");
            MinButton.ToolTip = s.Minimized ? "Expand: show everything" : "Compact: show only selected species";
            var full = s.Minimized ? Visibility.Collapsed : Visibility.Visible;
            var compact = s.Minimized ? Visibility.Visible : Visibility.Collapsed;
            InfoRow.Visibility = full;      // map name + time row
            FooterBar.Visibility = full;
            TitleText.Visibility = full;    // "LIVE NEED ZONES"
            MapButton.Visibility = full;    // Select Map
            TitleTime.Visibility = compact; // time centred in the title bar
        }

        // ---------- moving ----------

        void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1) return;
            try { DragMove(); } catch (InvalidOperationException) { return; }
            s.X = Left;
            s.Y = Top;
            s.Save();
        }

        // ---------- maps ----------

        Reserve Viewing => reserves.TryGetValue(s.Reserve ?? "", out var r) ? r : null;

        void MapButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = MapButton,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 4,
            };
            if (reserves.Count == 0)
                menu.Items.Add(new MenuItem { Header = "(no maps in data folder)", IsEnabled = false });
            foreach (var r in reserves.Values.OrderBy(r => r.Short))
            {
                var item = new MenuItem { Header = (r.Id == s.Reserve ? "✓  " : "     ") + r.Short };
                string id = r.Id;
                item.Click += (_, __) => PickMap(id);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        void PickMap(string id)
        {
            s.Reserve = id;
            s.Save();
            RenderRows();
            ApplySize();
        }

        // ---------- table ----------

        void RenderRows()
        {
            rows.Clear();
            var r = Viewing;
            HeadRow.Visibility = r != null && !s.Minimized ? Visibility.Visible : Visibility.Collapsed;
            EmptyMessage.Visibility = Visibility.Collapsed;
            if (r == null)
            {
                string msg = reserves.Count > 0 ? "Pick the map you're on from Select Map." : "No map data found in the data folder.";
                if (loadErrors.Count > 0) msg += "\n" + string.Join("\n", loadErrors);
                ShowMessage(msg);
                UpdateValues();
                return;
            }
            var pr = s.For(r.Id);
            var names = r.Species.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (s.ShowSelectedOnly || s.Minimized)
            {
                names = names.Where(n => pr.Selected.Contains(n)).ToList();
                if (names.Count == 0)
                    ShowMessage(s.Minimized
                        ? "No species selected.\nExpand (▾) and tick the species to track."
                        : "No species selected.\nTick some, or untick 'Show selected only'.");
            }
            foreach (var n in names)
                rows.Add(new RowVM(n, pr.Selected.Contains(n), !s.Minimized && pr.Collapsed.Contains(n), OnSelect));
            UpdateValues();
        }

        void ShowMessage(string msg)
        {
            EmptyMessage.Text = msg;
            EmptyMessage.Visibility = Visibility.Visible;
        }

        void OnSelect(RowVM row, bool on)
        {
            var r = Viewing;
            if (r == null) return;
            var pr = s.For(r.Id);
            pr.Selected.Remove(row.Name);
            if (on) pr.Selected.Add(row.Name);
            pr.Selected.Sort(StringComparer.Ordinal);
            s.Save();
            if ((s.ShowSelectedOnly || s.Minimized) && !on)
                Dispatcher.BeginInvoke(new Action(() => { RenderRows(); ApplySize(); }));
        }

        void SpeciesName_Click(object sender, MouseButtonEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is RowVM row)) return;
            if (s.Minimized) return;   // compact mode always shows times
            var r = Viewing;
            if (r == null) return;
            var pr = s.For(r.Id);
            row.Collapsed = !row.Collapsed;
            pr.Collapsed.Remove(row.Name);
            if (row.Collapsed) pr.Collapsed.Add(row.Name);
            s.Save();
        }

        void SetAllCollapsed(bool collapsed)
        {
            var r = Viewing;
            if (r == null) return;
            s.For(r.Id).Collapsed = collapsed ? r.Species.Keys.ToList() : new List<string>();
            s.Save();
            foreach (var row in rows) row.Collapsed = collapsed;
        }

        void UpdateValues()
        {
            if (MapName == null) return;
            var t = T;
            var r = Viewing;
            double? now = clock.Now();
            if (r == null)
            {
                MapName.Text = "No map selected";
                MapName.Foreground = Theme.Brush(t.Warn);
            }
            else
            {
                MapName.Text = r.Short;
                MapName.Foreground = Theme.Brush(t.Fg);
            }
            if (now == null)
            {
                TimeValue.Text = "not set — click ⏱";
                TimeValue.Foreground = Theme.Brush(t.Warn);
            }
            else
            {
                TimeValue.Text = Schedule.Fmt(now.Value);
                TimeValue.Foreground = Theme.Brush(t.Fg);
            }
            TitleTimeValue.Text = TimeValue.Text;           // compact-mode copy in the title bar
            TitleTimeValue.Foreground = TimeValue.Foreground;
            if (r == null) return;
            foreach (var row in rows)
            {
                if (now == null) { row.Refresh(null, null, false, t); continue; }
                var (cur, next) = Schedule.Status(r.Species[row.Name], now.Value);
                row.Refresh(cur, next, true, t);
            }
        }

        // ---------- settings popup ----------

        void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if ((DateTime.UtcNow - popupClosedAt).TotalMilliseconds < 250) return;  // click that just closed it
            RefreshPopup();
            SettingsPopup.IsOpen = true;
        }

        void SettingsPopup_Closed(object sender, EventArgs e) => popupClosedAt = DateTime.UtcNow;

        CustomPopupPlacement[] PlacePopupUnderRightEdge(Size popupSize, Size targetSize, Point offset) =>
            new[] { new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, targetSize.Height + 6),
                                             PopupPrimaryAxis.Horizontal) };

        void RefreshPopup()
        {
            updatingPopup = true;
            DarkPill.IsChecked = s.Theme == "dark";
            LightPill.IsChecked = s.Theme == "light";
            FontValue.Text = $"{s.TextSize} px";
            OpacityValue.Text = $"{Math.Round(s.Alpha * 100)}%";
            RefreshUpdateUi();
            updatingPopup = false;
        }

        void SetTheme(string name)
        {
            s.Theme = name;
            s.Save();
            ApplyTheme();
            RefreshPopup();
        }

        void DarkPill_Click(object sender, RoutedEventArgs e) => SetTheme("dark");
        void LightPill_Click(object sender, RoutedEventArgs e) => SetTheme("light");

        void ChangeText(int delta)
        {
            int v = Math.Clamp(s.TextSize + delta, Settings.TextMin, Settings.TextMax);
            if (v == s.TextSize) return;
            s.TextSize = v;
            s.Save();
            ApplyTheme();
            ApplySize();
            RefreshPopup();
        }

        void FontDown_Click(object sender, RoutedEventArgs e) => ChangeText(-1);
        void FontUp_Click(object sender, RoutedEventArgs e) => ChangeText(1);

        void ChangeAlpha(double delta)
        {
            s.Alpha = Math.Round(Math.Clamp(s.Alpha + delta, 0.3, 1.0), 2);
            Opacity = s.Alpha;
            s.Save();
            RefreshPopup();
        }

        void OpacityDown_Click(object sender, RoutedEventArgs e) => ChangeAlpha(-0.05);
        void OpacityUp_Click(object sender, RoutedEventArgs e) => ChangeAlpha(0.05);

        // "Show selected only" checkbox under the map name
        void SelectedOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (updatingPopup) return;
            s.ShowSelectedOnly = (sender as CheckBox)?.IsChecked == true;
            s.Save();
            RenderRows();
            ApplySize();
        }

        void SyncSelectedOnlyChecks()
        {
            updatingPopup = true;
            MainSelectedOnlyCheck.IsChecked = s.ShowSelectedOnly;
            updatingPopup = false;
        }

        void CollapseAll_Click(object sender, RoutedEventArgs e) => SetAllCollapsed(true);
        void ExpandAll_Click(object sender, RoutedEventArgs e) => SetAllCollapsed(false);

        // ---------- updates (from GitHub releases) ----------

        async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (updating) return;
            if (pendingUpdate == null) await CheckForUpdateAsync(silent: false);
            else await InstallUpdateAsync(pendingUpdate);
        }

        async Task CheckForUpdateAsync(bool silent)
        {
            if (!Updater.IsConfigured)
            {
                if (!silent) ShowUpdateStatus("Add your GitHub repo to Updater.cs to turn on updates.");
                return;
            }
            if (!silent)
            {
                UpdateButton.IsEnabled = false;
                UpdateButton.Content = "Checking…";
                ShowUpdateStatus("");
            }
            try
            {
                var latest = await Updater.GetLatestAsync();
                pendingUpdate = Updater.IsNewer(latest.Version) ? latest : null;
                if (!silent)
                    ShowUpdateStatus(pendingUpdate != null
                        ? $"Version {Updater.Text(latest.Version)} is available."
                        : $"You're up to date (v{Updater.CurrentText}).");
            }
            catch (Exception ex)
            {
                if (!silent) ShowUpdateStatus("Couldn't check for updates: " + Updater.Friendly(ex));
            }
            finally
            {
                UpdateButton.IsEnabled = true;
                RefreshUpdateUi();
            }
        }

        void RefreshUpdateUi()
        {
            VersionText.Text = $"v{Updater.CurrentText}";
            UpdateDot.Visibility = pendingUpdate != null ? Visibility.Visible : Visibility.Collapsed;
            if (pendingUpdate != null)
            {
                UpdateButton.Content = $"Update to v{Updater.Text(pendingUpdate.Version)}";
                UpdateButton.Foreground = Theme.Brush(T.Accent);
            }
            else
            {
                UpdateButton.Content = "Check for updates";
                UpdateButton.ClearValue(ForegroundProperty);
            }
        }

        void ShowUpdateStatus(string text)
        {
            UpdateStatus.Text = text;
            UpdateStatus.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        async Task InstallUpdateAsync(Updater.Release r)
        {
            if (string.IsNullOrEmpty(r.ZipUrl))
            {
                Updater.OpenUrl(r.PageUrl);
                ShowUpdateStatus("That release has no .zip attached, so its page was opened instead.");
                return;
            }
            SettingsPopup.IsOpen = false;
            var answer = MessageBox.Show(this,
                $"Update from v{Updater.CurrentText} to v{Updater.Text(r.Version)}?\n\n" +
                "The update will download and the overlay will restart. Your settings are kept.",
                "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            updating = true;
            FooterText.Text = "Downloading update…";
            var progress = new Progress<double>(p => FooterText.Text = $"Downloading update… {p:P0}");
            try
            {
                await Updater.InstallAsync(r, progress);
                FooterText.Text = "Restarting…";
                Updater.Restart();
                ExitApp();
            }
            catch (Exception ex)
            {
                updating = false;
                UpdateFooter();
                MessageBox.Show(this, "The update didn't install: " + Updater.Friendly(ex) +
                                "\n\nYou can download it yourself from:\n" + r.PageUrl,
                                "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- time sync ----------

        void SyncButton_Click(object sender, RoutedEventArgs e) => OpenSync();

        void OpenSync()
        {
            if (!IsVisible) ShowOverlay();
            if (syncWindow != null && syncWindow.IsVisible)
            {
                syncWindow.Activate();
                return;
            }
            syncWindow = new SyncWindow(clock.Now(), Native.NiceHotkey(s.HotkeySyncClock)) { Owner = this, Left = Left + 24, Top = Top + 48 };
            syncWindow.Synced += minute =>
            {
                clock.Sync(minute);
                s.Save();
                UpdateValues();
            };
            syncWindow.Show();
            syncWindow.Activate();
        }

        // ---------- click-through, visibility ----------

        void OnSourceInitialized(object sender, EventArgs e)
        {
            hwnd = new WindowInteropHelper(this).Handle;
            hwndSource = HwndSource.FromHwnd(hwnd);
            hwndSource.AddHook(WndProc);
            ApplyClickThrough();
            RegisterHotkeys();
            StartTray();
        }

        void ApplyClickThrough()
        {
            if (hwnd == IntPtr.Zero) return;
            long ex = Native.GetExStyle(hwnd) | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;
            ex = interactive ? (ex & ~Native.WS_EX_TRANSPARENT) : (ex | Native.WS_EX_TRANSPARENT);
            Native.SetExStyle(hwnd, ex);
            UpdateFooter();
        }

        void ToggleInteractive()
        {
            if (!IsVisible) ShowOverlay();
            interactive = !interactive;
            ApplyClickThrough();
        }

        void ToggleVisible()
        {
            if (IsVisible) Hide();
            else ShowOverlay();
        }

        void ShowOverlay()
        {
            Show();
            Topmost = false;
            Topmost = true;
            ApplyClickThrough();
        }

        void UpdateFooter()
        {
            string hk = Native.NiceHotkey(s.HotkeyToggleInteract);
            FooterText.Text = interactive ? $"{hk}: UNLOCKED" : $"{hk} · LOCKED";
            FooterText.ToolTip = interactive ? $"Press {hk} to let clicks pass through to the game"
                                             : $"Press {hk} to use the overlay's buttons";
        }

        // ---------- hotkeys ----------

        void RegisterHotkeys()
        {
            var failed = false;
            foreach (var (id, text) in new[] { (HkVisible, s.HotkeyToggleVisible), (HkInteract, s.HotkeyToggleInteract),
                                               (HkSync, s.HotkeySyncClock) })
            {
                var parsed = Native.ParseHotkey(text);
                if (parsed == null) continue;
                if (!Native.RegisterHotKey(hwnd, id, parsed.Value.mods | Native.MOD_NOREPEAT, parsed.Value.vk)) failed = true;
            }
            if (failed)
            {
                FooterText.Text = "Hotkey in use";
                FooterText.ToolTip = "A hotkey is used by another app — change it in settings-wpf.json";
            }
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                switch (wParam.ToInt32())
                {
                    case HkVisible: ToggleVisible(); break;
                    case HkInteract: ToggleInteractive(); break;
                    case HkSync: OpenSync(); break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        // ---------- tray ----------

        void StartTray()
        {
            if (!s.TrayIcon) return;
            tray = new System.Windows.Forms.NotifyIcon
            {
                Text = "CoTW Need Zones (running)",
                Icon = File.Exists(IconPath) ? new System.Drawing.Icon(IconPath) : System.Drawing.SystemIcons.Application,
                Visible = true,
            };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Show / hide overlay", null, (_, __) => Dispatcher.Invoke(ToggleVisible));
            menu.Items.Add("Toggle click-through", null, (_, __) => Dispatcher.Invoke(ToggleInteractive));
            menu.Items.Add("Sync in-game time…", null, (_, __) => Dispatcher.Invoke(OpenSync));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) => Dispatcher.Invoke(ExitApp));
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (_, ev) =>
            {
                if (ev.Button == System.Windows.Forms.MouseButtons.Left) Dispatcher.Invoke(ToggleVisible);
            };
        }

        // ---------- exit ----------

        void CloseButton_Click(object sender, RoutedEventArgs e) => ExitApp();

        void ExitApp()
        {
            if (exiting) return;
            exiting = true;
            s.X = Left;
            s.Y = Top;
            s.Save();
            if (hwnd != IntPtr.Zero)
                foreach (var id in new[] { HkVisible, HkInteract, HkSync }) Native.UnregisterHotKey(hwnd, id);
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
            }
            Application.Current.Shutdown();
        }
    }
}
