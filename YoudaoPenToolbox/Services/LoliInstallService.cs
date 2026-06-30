using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class LoliInstallService
    {
        private const string GiteeApiBase = "https://gitee.com/api/v5/repos/yanda2008/youdao-pen-loli/contents";
        private const string GiteeRawBase = "https://gitee.com/yanda2008/youdao-pen-loli/raw/main";
        private const int DownloadAttemptTimeoutSeconds = 180;
        private const int DownloadMaxAttempts = 3;

        private readonly DevicePlatformService _platformService;

        public LoliInstallService(DevicePlatformService platformService)
        {
            _platformService = platformService;
        }

        public Task<LoliPlatformInfo> DetectPlatformAsync(string serial)
        {
            return _platformService.DetectLoliPlatformAsync(serial);
        }

        public Task<LoliPlatformInfo> BuildPlatformFromUserChoiceAsync(string serial, bool useRockchip)
        {
            return _platformService.BuildFromUserChoiceAsync(serial, useRockchip);
        }

        public async Task<LoliReleaseInfo> GetLatestReleaseAsync(LoliPlatformInfo platform)
        {
            if (platform == null || !platform.IsSupported)
            {
                throw new InvalidOperationException("当前设备平台不受支持，无法选择 Loli 安装包。");
            }

            var apiUrl = $"{GiteeApiBase}/{platform.RepositorySubPath}?ref=main";
            using (var client = CreateHttpClient(TimeSpan.FromSeconds(30)))
            {
                var json = await client.GetStringAsync(apiUrl).ConfigureAwait(false);
                var entries = JArray.Parse(json);

                LoliReleaseInfo latest = null;
                foreach (var entry in entries)
                {
                    if (!string.Equals(entry["type"]?.ToString(), "file", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var name = entry["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".amr", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!name.StartsWith("loli_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var version = ParseLoliVersion(name);
                    if (version == null)
                    {
                        continue;
                    }

                    if (latest == null || version > latest.Version)
                    {
                        var apiDownloadUrl = entry["download_url"]?.ToString();
                        latest = new LoliReleaseInfo
                        {
                            FileName = name,
                            Version = version,
                            VersionText = version.ToString(),
                            DownloadUrl = NormalizeRawDownloadUrl(apiDownloadUrl, platform.RepositorySubPath, name),
                            RepositoryPath = $"{platform.RepositorySubPath}/{name}",
                            Platform = platform
                        };
                    }
                }

                if (latest == null)
                {
                    throw new InvalidOperationException($"在 {platform.RepositorySubPath} 未找到 Loli 安装包。");
                }

                return latest;
            }
        }

        public async Task<string> DownloadReleaseAsync(
            LoliReleaseInfo release,
            IProgress<DownloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            var cacheDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "loli");
            Directory.CreateDirectory(cacheDir);

            var localPath = Path.Combine(cacheDir, release.FileName);
            if (IsValidAmrFile(localPath))
            {
                progress?.Report(new DownloadProgress
                {
                    BytesReceived = new FileInfo(localPath).Length,
                    TotalBytes = new FileInfo(localPath).Length
                });
                release.LocalPath = localPath;
                return localPath;
            }

            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }

            var downloadUrl = NormalizeRawDownloadUrl(
                release.DownloadUrl,
                release.Platform?.RepositorySubPath,
                release.FileName);

            Exception lastError = null;
            for (var attempt = 1; attempt <= DownloadMaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using (var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        attemptCts.CancelAfter(TimeSpan.FromSeconds(DownloadAttemptTimeoutSeconds));
                        await DownloadToFileAsync(downloadUrl, localPath, progress, attemptCts.Token)
                            .ConfigureAwait(false);
                    }

                    if (!IsValidAmrFile(localPath))
                    {
                        throw new InvalidOperationException("下载内容不是有效的 AMR 安装包，可能是网络拦截或链接失效。");
                    }

                    release.LocalPath = localPath;
                    release.DownloadUrl = downloadUrl;
                    return localPath;
                }
                catch (Exception ex) when (attempt < DownloadMaxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    lastError = ex;
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"下载 {release.FileName} 失败（已重试 {DownloadMaxAttempts} 次）: {lastError?.Message}",
                lastError);
        }

        private static async Task DownloadToFileAsync(
            string downloadUrl,
            string localPath,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            using (var client = CreateHttpClient(TimeSpan.FromMinutes(5)))
            using (var response = await client
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
        }

        private static bool IsValidAmrFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var info = new FileInfo(path);
            if (info.Length < 4096)
            {
                return false;
            }

            using (var stream = File.OpenRead(path))
            {
                var header = new byte[2];
                if (stream.Read(header, 0, 2) != 2)
                {
                    return false;
                }

                return header[0] == 0x50 && header[1] == 0x4B;
            }
        }

        private static Version ParseLoliVersion(string fileName)
        {
            var match = Regex.Match(fileName, @"loli_v([\d.]+)\.amr", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
        }

        private static string BuildRawDownloadUrl(string repositorySubPath, string fileName)
        {
            var subPath = (repositorySubPath ?? string.Empty).Trim().Trim('/');
            return $"{GiteeRawBase}/{subPath}/{fileName}";
        }

        private static string NormalizeRawDownloadUrl(string url, string repositorySubPath, string fileName)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (url.IndexOf("/blob/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return url.Replace("/blob/", "/raw/");
                }

                if (url.IndexOf("/raw/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return url;
                }
            }

            return BuildRawDownloadUrl(repositorySubPath, fileName);
        }

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var client = new HttpClient(handler)
            {
                Timeout = timeout
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YoudaoPenToolbox/1.0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            return client;
        }
    }

    public class LoliReleaseInfo
    {
        public string FileName { get; set; }
        public Version Version { get; set; }
        public string VersionText { get; set; }
        public string DownloadUrl { get; set; }
        public string RepositoryPath { get; set; }
        public string LocalPath { get; set; }
        public LoliPlatformInfo Platform { get; set; }

        public string Summary =>
            $"{Platform?.PlatformLabel} · v{VersionText} · {FileName}";
    }
}
