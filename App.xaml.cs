using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace NeedZones
{
    public partial class App : Application
    {
        static Mutex singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Show startup errors instead of leaving an invisible process in Task Manager
            DispatcherUnhandledException += (_, ev) =>
            {
                ev.Handled = true;
                Report(ev.Exception);
                Shutdown();
            };
            AppDomain.CurrentDomain.UnhandledException += (_, ev) => Report(ev.ExceptionObject as Exception);

            // Only one copy at a time (a second copy can't register the hotkeys).
            // After an update, the new copy waits for the old one to finish closing.
            bool afterUpdate = e.Args.Contains("--after-update");
            singleInstance = new Mutex(false, "CotwNeedOverlay_SingleInstance");
            bool isFirst;
            try { isFirst = singleInstance.WaitOne(afterUpdate ? TimeSpan.FromSeconds(15) : TimeSpan.Zero); }
            catch (AbandonedMutexException) { isFirst = true; }   // the old copy exited without releasing it
            if (!isFirst)
            {
                MessageBox.Show("The Need Zones overlay is already running.\n\n" +
                                "Press Ctrl+Alt+H to show it, or use its icon by the Windows clock " +
                                "(it may be under the ^ arrow).\n\nTo restart it, end CotwNeedOverlay in Task Manager first.",
                                "CoTW Need Zones", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            try
            {
                // Fonts compiled into the exe (fonts\*.otf / *.ttf), with Windows fallbacks
                var baseUri = new Uri("pack://application:,,,/");
                Resources["BodyFont"] = new FontFamily(baseUri, "./fonts/#Inter, Segoe UI");
                Resources["TitleFont"] = new FontFamily(baseUri,
                    "./fonts/#Barlow Condensed, Bahnschrift SemiBold Condensed, Bahnschrift");
                var w = new MainWindow();
                w.Show();
                w.Activate();
            }
            catch (Exception ex)
            {
                Report(ex);
                Shutdown();
            }
        }

        static void Report(Exception ex)
        {
            if (ex == null) return;
            string path = "";
            try
            {
                path = Path.Combine(Settings.ConfigDir, "error.log");
                File.WriteAllText(path, $"{DateTime.Now}\n{ex}");
            }
            catch (Exception) { }
            MessageBox.Show("The Need Zones overlay hit an error and has to close:\n\n" + ex.Message +
                            (path.Length > 0 ? "\n\nFull details were saved to:\n" + path : ""),
                            "CoTW Need Zones", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
