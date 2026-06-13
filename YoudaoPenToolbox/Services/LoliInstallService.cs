using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class LoliInstallService
    {
        private const string GiteeApiBase = "https://gitee.com/api/v5/repos/yanda2008/youdao-pen-loli/contents";
        private const string GiteeRawBase = "https://gitee.com/yanda2008/youdao-pen-loli/raw/main";

        private readonly DevicePlatformService _platformService;
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public LoliInstallService(DevicePlatformService platformService)
        {
            _platformService = platformService;
        }

        public Task<LoliPlatformInfo> DetectPlatformAsync(string serial)
        {
            return _platformService.DetectLoliPlatformAsync(serial);
        }

        public async Task<LoliReleaseInfo> GetLatestReleaseAsync(LoliPlatformInfo platform)
        {
            if (platform == null || !platform.IsSupported)
            {
                throw new InvalidOperationException("当前设备平台不受支持，无法选择 Loli 安装包。");
            }

            var apiUrl = $"{GiteeApiBase}/{platform.RepositorySubPath}?ref=main";
            var json = await HttpClient.GetStringAsync(apiUrl).ConfigureAwait(false);
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
                    latest = new LoliReleaseInfo
                    {
                        FileName = name,
                        Version = version,
                        VersionText = version.ToString(),
                        DownloadUrl = entry["download_url"]?.ToString()
                            ?? $"{GiteeRawBase}/{platform.RepositorySubPath}/{name}",
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

        public async Task<string> DownloadReleaseAsync(LoliReleaseInfo release)
        {
            if (release == null)
            {
                throw new ArgumentNullException(nameof(release));
            }

            var cacheDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "loli");
            Directory.CreateDirectory(cacheDir);

            var localPath = Path.Combine(cacheDir, release.FileName);
            using (var response = await HttpClient.GetAsync(release.DownloadUrl).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var file = File.Create(localPath))
                {
                    await stream.CopyToAsync(file).ConfigureAwait(false);
                }
            }

            release.LocalPath = localPath;
            return localPath;
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

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YoudaoPenToolbox/1.0");
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
