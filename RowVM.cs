using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace NeedZones
{
    /// <summary>One species row in the table.</summary>
    public sealed class RowVM : INotifyPropertyChanged
    {
        readonly Action<RowVM, bool> onSelect;
        bool selected, collapsed;
        string nowNeed = "—", nowTimes = "", nextNeed = "—", nextTimes = "";
        Brush nowBrush, nextBrush;

        public RowVM(string name, bool selected, bool collapsed, Action<RowVM, bool> onSelect)
        {
            Name = name;
            this.selected = selected;
            this.collapsed = collapsed;
            this.onSelect = onSelect;
        }

        public string Name { get; }

        public bool Selected
        {
            get => selected;
            set { if (selected != value) { selected = value; Changed(); onSelect?.Invoke(this, value); } }
        }

        public bool Collapsed
        {
            get => collapsed;
            set { if (collapsed != value) { collapsed = value; Changed(); Changed(nameof(TimesVisibility)); } }
        }

        public Visibility TimesVisibility => collapsed ? Visibility.Collapsed : Visibility.Visible;

        public string NowNeed { get => nowNeed; set => Set(ref nowNeed, value); }
        public string NowTimes { get => nowTimes; set => Set(ref nowTimes, value); }
        public string NextNeed { get => nextNeed; set => Set(ref nextNeed, value); }
        public string NextTimes { get => nextTimes; set => Set(ref nextTimes, value); }
        public Brush NowBrush { get => nowBrush; set => Set(ref nowBrush, value); }
        public Brush NextBrush { get => nextBrush; set => Set(ref nextBrush, value); }

        public void Refresh(Segment current, Segment next, bool timeSet, Theme t)
        {
            NowNeed = current?.Need ?? "—";
            NowTimes = current != null ? Schedule.Times(current) : (timeSet ? "no need zone" : "");
            NowBrush = Theme.Brush(t.NeedColor(current?.Need));
            NextNeed = next?.Need ?? "—";
            NextTimes = next != null ? Schedule.Times(next) : "";
            // the Next column sits at ~75% strength, like the mockup
            NextBrush = Theme.Brush(Theme.Mix(t.NeedColor(next?.Need), t.Bg, 0.25));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void Changed([CallerMemberName] string prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        void Set<T>(ref T field, T value, [CallerMemberName] string prop = null)
        {
            if (Equals(field, value)) return;
            field = value;
            Changed(prop);
        }
    }
}
