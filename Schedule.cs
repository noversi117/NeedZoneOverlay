using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NeedZones
{
    /// <summary>One stretch of the day: start minute, length in minutes, need (null = no need zone).</summary>
    public sealed class Segment
    {
        public int Start { get; }
        public int Length { get; }
        public string Need { get; }
        public int End => (Start + Length) % Schedule.Day;

        public Segment(int start, int length, string need)
        {
            Start = start;
            Length = length;
            Need = need;
        }
    }

    public sealed class Reserve
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Short { get; set; }
        public Dictionary<string, List<Segment>> Species { get; } = new Dictionary<string, List<Segment>>();
    }

    public static class Schedule
    {
        public const int Day = 1440;
        public static readonly string[] Needs = { "Feed", "Drink", "Rest", "Den", "Swim", "Gather" };

        public static int ParseHhmm(string text)
        {
            var parts = text.Trim().Split(':');
            return (int.Parse(parts[0]) % 24) * 60 + int.Parse(parts[1]);
        }

        public static string Fmt(double minute)
        {
            int m = ((int)Math.Floor(minute) % Day + Day) % Day;
            return $"{m / 60:00}:{m % 60:00}";
        }

        /// <summary>Merge raw windows into whole-day segments; gaps get Need = null. Ordered from a boundary.</summary>
        public static List<Segment> BuildSegments(IEnumerable<(string start, string end, string need)> windows)
        {
            var timeline = new string[Day];
            foreach (var (start, end, need) in windows)
            {
                if (Array.IndexOf(Needs, need) < 0)
                    throw new InvalidDataException($"Unknown need type '{need}' (use one of {string.Join(", ", Needs)})");
                int s = ParseHhmm(start), e = ParseHhmm(end);
                if (e <= s) e += Day;
                for (int m = s; m < e; m++) timeline[m % Day] = need;
            }

            int boundary = -1;
            for (int i = 0; i < Day; i++)
            {
                if (timeline[(i + Day - 1) % Day] != timeline[i]) { boundary = i; break; }
            }
            if (boundary < 0) return new List<Segment> { new Segment(0, Day, timeline[0]) };

            var segs = new List<Segment>();
            int segStart = boundary;
            string cur = timeline[boundary];
            for (int p = boundary + 1; p < boundary + Day; p++)
            {
                string t = timeline[p % Day];
                if (t != cur)
                {
                    segs.Add(new Segment(segStart % Day, p - segStart, cur));
                    segStart = p;
                    cur = t;
                }
            }
            segs.Add(new Segment(segStart % Day, boundary + Day - segStart, cur));
            return segs;
        }

        /// <summary>(current need window or null, next need window or null) at minute `now`.</summary>
        public static (Segment current, Segment next) Status(List<Segment> segs, double now)
        {
            int n = ((int)Math.Floor(now) % Day + Day) % Day;
            int idx = segs.FindIndex(s => ((n - s.Start) % Day + Day) % Day < s.Length);
            Segment current = segs[idx].Need != null ? segs[idx] : null;
            Segment next = null;
            for (int k = 1; k <= segs.Count; k++)
            {
                var seg = segs[(idx + k) % segs.Count];
                if (seg.Need != null) { next = seg; break; }
            }
            if (ReferenceEquals(next, current)) next = null;
            return (current, next);
        }

        public static string Times(Segment s) => $"{Fmt(s.Start)}–{Fmt(s.End)}";

        /// <summary>24-hour time only: "07:20", "7:20", "0720", "19:05", "07:" -> minute of day, or null.</summary>
        public static int? ParseUserTime(string text)
        {
            var m = Regex.Match(text ?? "", @"^\s*(\d{1,2}):?(\d{2})?\s*$");
            if (!m.Success) return null;
            int h = int.Parse(m.Groups[1].Value);
            int mins = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (h > 23 || mins > 59) return null;
            return h * 60 + mins;
        }

        /// <summary>Auto-format the time box as HH:MM: digits only, leading 0 added, ':' inserted.</summary>
        public static string FormatTimeEntry(string text)
        {
            var o = new StringBuilder();
            foreach (char ch in text ?? "")
            {
                if (!char.IsDigit(ch)) continue;
                int n = o.Length;
                if (n == 0)
                {
                    if (ch > '2') o.Append('0').Append(ch); else o.Append(ch);   // 3-9 can only be 03-09
                }
                else if (n == 1)
                {
                    string prev = o.ToString();
                    if (int.Parse(prev + ch) <= 23) o.Append(ch);
                    else { o.Clear(); o.Append('0').Append(prev).Append(ch); }  // "24" -> 02 then 4
                }
                else if (n == 2)
                {
                    if (ch <= '5') o.Append(ch);                                // minute tens: 0-5
                }
                else if (n == 3)
                {
                    o.Append(ch);
                }
                if (o.Length == 3 && o[2] > '5') o.Length = 2;
                if (o.Length >= 4) break;
            }
            string s = o.ToString();
            if (s.Length > 4) s = s.Substring(0, 4);
            if (s.Length < 2) return s;
            return s.Substring(0, 2) + ":" + s.Substring(2);
        }

        public static Reserve LoadReserve(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var root = doc.RootElement;
            var r = new Reserve
            {
                Id = root.GetProperty("id").GetString(),
                Name = root.GetProperty("name").GetString(),
                Short = root.TryGetProperty("short", out var sh) ? sh.GetString() : root.GetProperty("name").GetString(),
            };
            foreach (var sp in root.GetProperty("species").EnumerateObject())
            {
                var windows = sp.Value.EnumerateArray()
                    .Select(w => (w[0].GetString(), w[1].GetString(), w[2].GetString()))
                    .ToList();
                r.Species[sp.Name] = BuildSegments(windows);
            }
            return r;
        }

        public static (Dictionary<string, Reserve> reserves, List<string> errors) LoadReserves(string dataDir)
        {
            var reserves = new Dictionary<string, Reserve>();
            var errors = new List<string>();
            if (!Directory.Exists(dataDir))
            {
                errors.Add($"Data folder not found: {dataDir}");
                return (reserves, errors);
            }
            foreach (var file in Directory.GetFiles(dataDir, "*.json").OrderBy(f => f))
            {
                if (Path.GetFileName(file).Equals("reserves.json", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var r = LoadReserve(file);
                    reserves[r.Id] = r;
                }
                catch (Exception e)
                {
                    errors.Add($"{Path.GetFileName(file)}: {e.Message}");
                }
            }
            return (reserves, errors);
        }
    }
}
