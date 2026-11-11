using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace NeedZones
{
    /// <summary>Enter the in-game time (24h). Only digits are accepted; ':' and a leading 0 are added automatically.</summary>
    public partial class SyncWindow : Window
    {
        public event Action<int> Synced;

        public SyncWindow(double? now, string hotkey)
        {
            InitializeComponent();
            HotkeyTip.Text = $"Press {hotkey} to sync time.";
            DataObject.AddPastingHandler(TimeBox, (_, e) => e.CancelCommand());
            if (now.HasValue) TimeBox.Text = Schedule.Fmt(now.Value);
            Loaded += (_, __) =>
            {
                TimeBox.Focus();
                TimeBox.SelectAll();
            };
            MouseLeftButtonDown += (_, e) =>
            {
                if (TimeBox.IsMouseOver) return;
                try { DragMove(); } catch (InvalidOperationException) { }
            };
        }

        void SetText(string text)
        {
            TimeBox.Text = text;
            TimeBox.CaretIndex = TimeBox.Text.Length;
            ErrorText.Text = "";
        }

        string CurrentText() => TimeBox.SelectionLength > 0 ? "" : TimeBox.Text;

        void TimeBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (e.Text.Length > 0 && e.Text.All(char.IsDigit))
                SetText(Schedule.FormatTimeEntry(CurrentText() + e.Text));
        }

        void TimeBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Back:
                    e.Handled = true;
                    string digits = new string(CurrentText().Where(char.IsDigit).ToArray());
                    if (digits.Length > 0) digits = digits.Substring(0, digits.Length - 1);
                    SetText(Schedule.FormatTimeEntry(digits));
                    break;
                case Key.Delete:
                    e.Handled = true;
                    SetText("");
                    break;
                case Key.Space:
                    e.Handled = true;
                    break;
                case Key.Enter:
                    e.Handled = true;
                    Accept();
                    break;
                case Key.Escape:
                    e.Handled = true;
                    Close();
                    break;
            }
        }

        void TimeBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
            Placeholder.Visibility = TimeBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        void Accept()
        {
            int? minute = Schedule.ParseUserTime(TimeBox.Text);
            if (minute == null)
            {
                ErrorText.Text = "Enter a 24-hour time, like 07:20 or 19:05.";
                return;
            }
            Synced?.Invoke(minute.Value);
            Close();
        }

        void Sync_Click(object sender, RoutedEventArgs e) => Accept();
        void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
