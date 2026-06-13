using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace YoudaoPenToolbox.Services
{
    public class AppUpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public string RemoteVersionText { get; set; }
        public Version RemoteVersion { get; set; }
        public Version CurrentVersion { get; set; }
        public string DownloadUrl { get; set; }
    }

    public class DownloadProgress
    {
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }

        public double? Percent =>
            TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100 : null;
    }

    public class AppUpdateService
    {
        private const string VersionUrl = "https://gitee.com/yanda2008/penmirror/raw/master/UPDATE/version";
        private const string DownloadUrlTemplate =
            "https://gitee.com/yanda2008/penmirror/raw/master/UPDATE/BAN/YoudaoPenToolbox_V{0}.exe";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        public Version GetCurrentVersion()
        {
            return NormalizeVersion(Assembly.GetExecutingAssembly().GetName().Version);
        }

        public string GetCurrentVersionText()
        {
            return FormatVersionText(GetCurrentVersion());
        }

        public async Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            var remoteVersionText = (await HttpClient.GetStringAsync(VersionUrl).ConfigureAwait(false)).Trim();
            if (string.IsNullOrWhiteSpace(remoteVersionText))
            {
                throw new InvalidOperationException("远程版本号为空。");
            }

            var remoteVersion = ParseVersion(remoteVersionText);
            var currentVersion = GetCurrentVersion();

            return new AppUpdateCheckResult
            {
                HasUpdate = remoteVersion > currentVersion,
                RemoteVersionText = remoteVersionText,
                RemoteVersion = remoteVersion,
                CurrentVersion = currentVersion,
                DownloadUrl = string.Format(DownloadUrlTemplate, remoteVersionText)
            };
        }

        public async Task<string> DownloadUpdateAsync(
            string downloadUrl,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken = default)
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "update");
            Directory.CreateDirectory(cacheDir);

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var localPath = Path.Combine(cacheDir, fileName);

            using (var response = await HttpClient
                       .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var file = File.Create(localPath))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        await file.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        received += read;
                        progress?.Report(new DownloadProgress
                        {
                            BytesReceived = received,
                            TotalBytes = totalBytes
                        });
                    }
                }
            }

            return localPath;
        }

        public void ApplyUpdateAndRestart(string downloadedExePath)
        {
            if (string.IsNullOrWhiteSpace(downloadedExePath) || !File.Exists(downloadedExePath))
            {
                throw new FileNotFoundException("更新包不存在。", downloadedExePath);
            }

            var currentExe = Process.GetCurrentProcess().MainModule.FileName;
            var batchPath = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "apply_update.bat");
            Directory.CreateDirectory(Path.GetDirectoryName(batchPath) ?? Path.GetTempPath());

            var script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("timeout /t 2 /nobreak > nul");
            script.AppendLine($"copy /Y \"{downloadedExePath}\" \"{currentExe}\" > nul");
            script.AppendLine($"start \"\" \"{currentExe}\"");
            script.AppendLine($"del \"{batchPath}\"");

            File.WriteAllText(batchPath, script.ToString(), Encoding.Default);

            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Application.Current.Shutdown();
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(15)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YoudaoPenToolbox/1.0");
            return client;
        }

        private static Version ParseVersion(string text)
        {
            if (!Version.TryParse(text.Trim(), out var version))
            {
                throw new FormatException($"无法解析版本号: {text}");
            }

            return NormalizeVersion(version);
        }

        private static Version NormalizeVersion(Version version)
        {
            var build = version.Build < 0 ? 0 : version.Build;
            var revision = version.Revision < 0 ? 0 : version.Revision;
            return new Version(version.Major, version.Minor, build, revision);
        }

        private static string FormatVersionText(Version version)
        {
            var build = version.Build < 0 ? 0 : version.Build;
            var revision = version.Revision < 0 ? 0 : version.Revision;

            if (revision > 0)
            {
                return $"{version.Major}.{version.Minor}.{build}.{revision}";
            }

            return $"{version.Major}.{version.Minor}.{build}";
        }
    }
}
