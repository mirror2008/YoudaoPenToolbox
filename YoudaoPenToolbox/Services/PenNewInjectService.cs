using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YoudaoPenToolbox.Services
{
    public class PenNewInjectManifest
    {
        public int ShardCount { get; set; }
        public string ArchiveBaseName { get; set; }
    }

    public class PenNewInjectService
    {
        private const string ManifestUrl = "https://gitee.com/yanda2008/penmirror/raw/master/ADB/daxiao";
        private const string ShardRawBase = "https://gitee.com/yanda2008/penmirror/raw/master/ADB";
        private const string SevenZipExeUrl = "https://gitee.com/yanda2008/scpslv9.1.3/raw/master/7z/7z.exe";
        private const string SevenZipDllUrl = "https://gitee.com/yanda2008/scpslv9.1.3/raw/master/7z/7z.dll";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task<PenNewInjectManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
        {
            var text = await HttpClient.GetStringAsync(ManifestUrl).ConfigureAwait(false);
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                throw new InvalidOperationException("分片信息格式无效。");
            }

            if (!int.TryParse(lines[0].Trim(), out var shardCount) || shardCount <= 0)
            {
                throw new InvalidOperationException("分片数量无效。");
            }

            var archiveBaseName = lines[1].Trim();
            if (string.IsNullOrWhiteSpace(archiveBaseName))
            {
                throw new InvalidOperationException("分片名称无效。");
            }

            return new PenNewInjectManifest
            {
                ShardCount = shardCount,
                ArchiveBaseName = archiveBaseName
            };
        }

        public async Task<string> DownloadExtractAndGetExecutableAsync(
            IProgress<string> status,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken = default)
        {
            var manifest = await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
            var workDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "PenNewInject");
            var downloadDir = Path.Combine(workDir, "parts");
            var extractDir = Path.Combine(workDir, "app");
            Directory.CreateDirectory(downloadDir);
            Directory.CreateDirectory(extractDir);

            var existingExe = FindExecutable(extractDir, "PenNewInject.Ultra.exe");
            if (existingExe != null && AreAllShardsPresent(downloadDir, manifest))
            {
                status?.Report("已存在完整安装包，正在启动...");
                return existingExe;
            }

            for (var i = 1; i <= manifest.ShardCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var partName = $"{manifest.ArchiveBaseName}.{i:D3}";
                var localPath = Path.Combine(downloadDir, partName);
                status?.Report($"正在下载 {partName} ({i}/{manifest.ShardCount})...");
                await DownloadFileAsync(BuildShardUrl(partName), localPath, progress, cancellationToken)
                    .ConfigureAwait(false);
            }

            var firstPart = Path.Combine(downloadDir, $"{manifest.ArchiveBaseName}.001");
            await Extract7ZipAsync(firstPart, extractDir, status, progress, cancellationToken).ConfigureAwait(false);

            var exePath = FindExecutable(extractDir, "PenNewInject.Ultra.exe");
            if (exePath == null)
            {
                throw new FileNotFoundException("解压后未找到 PenNewInject.Ultra.exe。");
            }

            return exePath;
        }

        public void Launch(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                throw new FileNotFoundException("PenNewInject.Ultra.exe 不存在。", executablePath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
                UseShellExecute = true
            });
        }

        private static bool AreAllShardsPresent(string downloadDir, PenNewInjectManifest manifest)
        {
            for (var i = 1; i <= manifest.ShardCount; i++)
            {
                var partPath = Path.Combine(downloadDir, $"{manifest.ArchiveBaseName}.{i:D3}");
                if (!File.Exists(partPath) || new FileInfo(partPath).Length == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static async Task DownloadFileAsync(
            string url,
            string localPath,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            using (var response = await HttpClient
                       .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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

        private static async Task<(string ExePath, string WorkingDirectory)> Ensure7ZipToolsAsync(
            IProgress<string> status,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            var sevenZipDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "7z");
            var sevenZipExe = Path.Combine(sevenZipDir, "7z.exe");
            var sevenZipDll = Path.Combine(sevenZipDir, "7z.dll");

            if (IsValidFile(sevenZipExe) && IsValidFile(sevenZipDll))
            {
                return (sevenZipExe, sevenZipDir);
            }

            Directory.CreateDirectory(sevenZipDir);

            status?.Report("正在下载 7-Zip 解压组件 (7z.exe)...");
            await DownloadFileAsync(SevenZipExeUrl, sevenZipExe, progress, cancellationToken)
                .ConfigureAwait(false);

            status?.Report("正在下载 7-Zip 解压组件 (7z.dll)...");
            await DownloadFileAsync(SevenZipDllUrl, sevenZipDll, progress, cancellationToken)
                .ConfigureAwait(false);

            if (!IsValidFile(sevenZipExe) || !IsValidFile(sevenZipDll))
            {
                throw new InvalidOperationException("7-Zip 解压组件下载不完整，请检查网络后重试。");
            }

            return (sevenZipExe, sevenZipDir);
        }

        private static bool IsValidFile(string path)
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static async Task Extract7ZipAsync(
            string firstPartPath,
            string extractDir,
            IProgress<string> status,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            status?.Report("正在准备解压组件...");
            var (sevenZipExe, sevenZipDir) = await Ensure7ZipToolsAsync(status, progress, cancellationToken)
                .ConfigureAwait(false);

            status?.Report("正在解压...");
            var args = new StringBuilder();
            args.Append("x ");
            args.Append('"').Append(firstPartPath).Append('"');
            args.Append(" -o").Append('"').Append(extractDir).Append('"');
            args.Append(" -y");

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = sevenZipExe,
                    WorkingDirectory = sevenZipDir,
                    Arguments = args.ToString(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await Task.Run(() =>
                {
                    while (!process.HasExited)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!process.WaitForExit(200))
                        {
                            continue;
                        }
                    }
                }, cancellationToken).ConfigureAwait(false);

                await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                        ? $"7-Zip 解压失败，退出码 {process.ExitCode}。"
                        : $"7-Zip 解压失败: {error.Trim()}");
                }
            }
        }

        private static string FindExecutable(string rootDirectory, string fileName)
        {
            if (!Directory.Exists(rootDirectory))
            {
                return null;
            }

            return Directory.EnumerateFiles(rootDirectory, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }

        private static string BuildShardUrl(string fileName)
        {
            return $"{ShardRawBase}/{Uri.EscapeDataString(fileName)}";
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YoudaoPenToolbox/1.0");
            return client;
        }
    }
}
