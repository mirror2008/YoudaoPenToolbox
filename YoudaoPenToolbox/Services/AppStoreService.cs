using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class AppStoreService
    {
        public const string StoreBaseUrl = "https://store.tesbin.top";
        public const string UploadPortalUrl = StoreBaseUrl + "/web/";
        public const string CatalogUrl = StoreBaseUrl + "/api/apps";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task<IReadOnlyList<AppStoreItem>> GetCatalogAsync(CancellationToken cancellationToken = default)
        {
            using (var response = await HttpClient.GetAsync(CatalogUrl, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var root = JObject.Parse(json);
                var items = root["items"] as JArray ?? new JArray();
                var result = new List<AppStoreItem>();

                foreach (var token in items)
                {
                    if (token?["id"] == null)
                    {
                        continue;
                    }

                    var uploadedAt = ParseDate(token["uploadedAt"]?.ToString());
                    var sizeBytes = token["sizeBytes"]?.Value<long>() ?? 0;
                    result.Add(new AppStoreItem
                    {
                        Id = token["id"]?.ToString(),
                        Name = token["name"]?.ToString() ?? "未知应用",
                        AppId = token["appId"]?.ToString(),
                        Version = token["version"]?.ToString() ?? "-",
                        Platform = token["platform"]?.ToString() ?? "rk",
                        PlatformDisplay = token["platformDisplay"]?.ToString() ?? "RK · GStreamer",
                        PenModel = token["penModel"]?.ToString() ?? "-",
                        Description = token["description"]?.ToString() ?? "",
                        SizeBytes = sizeBytes,
                        SizeDisplay = AmrPackageInfo.FormatSize(sizeBytes),
                        UploadedAt = uploadedAt,
                        UploadedAtDisplay = uploadedAt == DateTime.MinValue
                            ? "-"
                            : uploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        Uploader = token["uploader"]?.ToString() ?? "-",
                        UploaderId = token["uploaderId"]?.Value<int>() ?? 0,
                        FileName = token["fileName"]?.ToString(),
                        IconUrl = token["iconUrl"]?.ToString(),
                        DownloadUrl = token["downloadUrl"]?.ToString()
                    });
                }

                return result;
            }
        }

        public async Task LoadIconAsync(AppStoreItem item, CancellationToken cancellationToken = default)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.IconUrl))
            {
                return;
            }

            item.IsIconLoading = true;
            try
            {
                using (var response = await HttpClient.GetAsync(item.IconUrl, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (bytes.Length == 0)
                    {
                        return;
                    }

                    var image = LoadBitmapFromBytes(bytes);
                    if (image != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => item.Icon = image);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() => item.IsIconLoading = false);
            }
        }

        public async Task<string> DownloadAppAsync(
            AppStoreItem item,
            IProgress<DownloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.DownloadUrl))
            {
                throw new ArgumentException("无效的应用下载地址。", nameof(item));
            }

            var cacheDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "appstore");
            Directory.CreateDirectory(cacheDir);

            var safeName = AppBackupService.SanitizeFileName(item.FileName ?? item.Id + ".amr");
            if (!safeName.EndsWith(".amr", StringComparison.OrdinalIgnoreCase))
            {
                safeName += ".amr";
            }

            var localPath = Path.Combine(cacheDir, safeName);

            using (var response = await HttpClient
                       .GetAsync(item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? item.SizeBytes;
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var file = File.Create(localPath))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
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

            if (!File.Exists(localPath) || new FileInfo(localPath).Length == 0)
            {
                throw new InvalidOperationException("下载的安装包无效。");
            }

            return localPath;
        }

        public void OpenUploadPortal()
        {
            Process.Start(new ProcessStartInfo(UploadPortalUrl) { UseShellExecute = true });
        }

        private static DateTime ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParse(text, out var parsed))
            {
                return parsed.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : parsed.ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        private static BitmapSource LoadBitmapFromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();
                return frame;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YoudaoPenToolbox/AppStore");
            return client;
        }
    }
}
