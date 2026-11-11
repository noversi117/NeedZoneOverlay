using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeedZones
{
    /// <summary>
    /// Updates the app from the latest GitHub release. A release needs a version tag (e.g. v1.0.1)
    /// and a .zip attached that holds the published files (the exe, data folder and icon.ico).
    /// </summary>
    public static class Updater
    {
        // ↓↓↓ Your GitHub repository as "username/repo-name". The repo must be public. ↓↓↓
        public const string Repo = "noversi117/NeedZoneOverlay";

        public static bool IsConfigured => !Repo.Contains("YOUR-");

        public sealed class Release
        {
            public Version Version;
            public string Tag;
            public string Notes;
            public string ZipUrl;
            public string PageUrl;
        }

        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("LiveNeedZones-Updater");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public static Version Current => Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0));

        public static string Text(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";

        public static string CurrentText => Text(Current);

        static Version Normalize(Version v) =>
            new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build), Math.Max(0, v.Revision));

        /// <summary>"v1.2.0", "1.2", "v2" -> Version; false if the tag isn't a version.</summary>
        public static bool TryParseVersion(string tag, out Version v)
        {
            v = null;
            string t = (tag ?? "").Trim().TrimStart('v', 'V');
            int cut = t.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut >= 0) t = t.Substring(0, cut);
            if (Version.TryParse(t, out var parsed)) { v = Normalize(parsed); return true; }
            if (int.TryParse(t, out int major)) { v = new Version(major, 0, 0, 0); return true; }
            return false;
        }

        public static bool IsNewer(Version latest) => Normalize(latest) > Current;

        public static async Task<Release> GetLatestAsync()
        {
            string json = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string tag = root.GetProperty("tag_name").GetString();
            if (!TryParseVersion(tag, out var version))
                throw new InvalidDataException($"The latest release's tag \"{tag}\" isn't a version like v1.0.1.");
            string zip = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string name = a.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zip = a.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
            return new Release
            {
                Version = version,
                Tag = tag,
                Notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                ZipUrl = zip,
                PageUrl = root.TryGetProperty("html_url", out var url) ? url.GetString() : $"https://github.com/{Repo}/releases",
            };
        }

        /// <summary>Download the release zip and put its files next to the running exe.</summary>
        public static async Task InstallAsync(Release r, IProgress<double> progress)
        {
            string appDir = AppContext.BaseDirectory;
            string exe = Environment.ProcessPath;
            string work = Path.Combine(Path.GetTempPath(), "LiveNeedZonesUpdate");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            Directory.CreateDirectory(work);

            // 1. download
            string zipPath = Path.Combine(work, "update.zip");
            using (var resp = await Http.GetAsync(r.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                long? total = resp.Content.Headers.ContentLength;
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(zipPath);
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await dst.WriteAsync(buffer, 0, n);
                    read += n;
                    if (total > 0) progress?.Report((double)read / total.Value);
                }
            }

            // 2. unzip (if everything sits in one top-level folder, use that folder)
            string extracted = Path.Combine(work, "files");
            ZipFile.ExtractToDirectory(zipPath, extracted);
            string root = extracted;
            var subdirs = Directory.GetDirectories(extracted);
            if (Directory.GetFiles(extracted).Length == 0 && subdirs.Length == 1) root = subdirs[0];
            if (!Directory.GetFiles(root, "*.exe").Any())
                throw new InvalidDataException("The release's .zip doesn't contain the app's .exe.");

            // 3. swap files. A running exe can't be overwritten, but it can be renamed out of the way.
            string oldExe = exe + ".old";
            if (File.Exists(oldExe)) File.Delete(oldExe);
            File.Move(exe, oldExe);
            try
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(root, file);
                    bool topLevelExe = Path.GetDirectoryName(rel) == "" &&
                                       Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase);
                    string target = topLevelExe ? exe : Path.Combine(appDir, rel);   // keeps your exe's name
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                }
            }
            catch
            {
                if (!File.Exists(exe) && File.Exists(oldExe)) File.Move(oldExe, exe);   // roll back
                throw;
            }
            try { Directory.Delete(work, true); } catch (Exception) { }
        }

        /// <summary>Start the updated exe; it waits for this copy to close before showing.</summary>
        public static void Restart() =>
            Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--after-update")
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            });

        /// <summary>Remove the previous exe left behind by an update.</summary>
        public static void CleanupOldFiles()
        {
            try
            {
                string old = Environment.ProcessPath + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch (Exception) { }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { }
        }

        public static string Friendly(Exception ex)
        {
            switch (ex)
            {
                case HttpRequestException h when h.StatusCode == HttpStatusCode.NotFound:
                    return $"no releases found at github.com/{Repo}. Check the name in Updater.cs and that the repo is public.";
                case HttpRequestException h when h.StatusCode == HttpStatusCode.Forbidden:
                    return "GitHub is limiting requests right now. Try again in a while.";
                case HttpRequestException _:
                    return "couldn't reach GitHub. Check your internet connection.";
                case TaskCanceledException _:
                    return "GitHub took too long to answer. Try again.";
                case UnauthorizedAccessException _:
                    return "Windows blocked writing to the app's folder. Move the app out of Program Files, or run it as administrator.";
                default:
                    return ex.Message;
            }
        }
    }
}
