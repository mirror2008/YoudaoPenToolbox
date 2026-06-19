using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class AppBackupResult
    {
        public bool Success { get; set; }
        public string LocalAmrPath { get; set; }
        public string Message { get; set; }
        public AmrPackageInfo PackageInfo { get; set; }
        public int FileCount { get; set; }
        public long ArchiveSizeBytes { get; set; }
    }

    public class AppBackupService
    {
        private readonly AdbService _adbService;
        private readonly PackageService _packageService;

        public AppBackupService(AdbService adbService, PackageService packageService)
        {
            _adbService = adbService;
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        }

        public async Task<AppBackupResult> BackupToAmrAsync(string serial, InstalledApp app, string localAmrPath)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (string.IsNullOrWhiteSpace(localAmrPath))
            {
                throw new ArgumentNullException(nameof(localAmrPath));
            }

            var installPath = await ResolveInstallPathAsync(serial, app).ConfigureAwait(false);
            var remoteZip = $"/tmp/ypt_backup_{app.AppId}_{DateTime.Now.Ticks}.amr";
            var quotedInstall = RemotePathHelper.ShellQuote(installPath);
            var quotedZip = RemotePathHelper.ShellQuote(remoteZip);

            try
            {
                await _adbService.ShellAsync(serial, $"rm -f {quotedZip}").ConfigureAwait(false);

                var zipResult = await _adbService.ShellAsync(serial,
                    $"cd {quotedInstall} && zip -rq {quotedZip} . && echo ZIP_OK").ConfigureAwait(false);
                EnsureZipSuccess(zipResult);

                var sizeText = await _adbService.ShellAsync(serial,
                    $"stat -c %s {quotedZip} 2>/dev/null || wc -c < {quotedZip}").ConfigureAwait(false);
                var remoteSize = ParseLong(sizeText);

                var entryCountText = await _adbService.ShellAsync(serial,
                    $"unzip -l {quotedZip} 2>/dev/null | tail -1").ConfigureAwait(false);
                var entryCount = ParseZipEntryCount(entryCountText);

                var manifestCheck = await _adbService.ShellAsync(serial,
                    $"unzip -l {quotedZip} 2>/dev/null | grep -F manifest.json").ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(manifestCheck)
                    || manifestCheck.IndexOf("manifest.json", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidOperationException("打包结果中未找到 manifest.json，无法生成有效 AMR");
                }

                var localDir = Path.GetDirectoryName(localAmrPath);
                if (!string.IsNullOrWhiteSpace(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                if (File.Exists(localAmrPath))
                {
                    File.Delete(localAmrPath);
                }

                var pulled = await _adbService.PullFileAsync(serial, remoteZip, localAmrPath).ConfigureAwait(false);
                if (!pulled || !File.Exists(localAmrPath))
                {
                    throw new InvalidOperationException("拉取备份包到电脑失败");
                }

                var packageInfo = AmrPackageService.Parse(localAmrPath);
                if (!packageInfo.IsValid)
                {
                    throw new InvalidOperationException(packageInfo.ErrorMessage ?? "生成的 AMR 无效");
                }

                var sizeSummary = remoteSize > 0
                    ? AmrPackageInfo.FormatSize(remoteSize)
                    : AmrPackageInfo.FormatSize(new FileInfo(localAmrPath).Length);
                var countSummary = entryCount > 0 ? $"{entryCount} 个文件" : "已打包";
                var archiveSize = remoteSize > 0 ? remoteSize : new FileInfo(localAmrPath).Length;
                var successMessage = $"备份成功：{Path.GetFileName(localAmrPath)}（{countSummary}，{sizeSummary}）";

                if (!string.IsNullOrWhiteSpace(packageInfo.ErrorMessage))
                {
                    return new AppBackupResult
                    {
                        Success = true,
                        LocalAmrPath = localAmrPath,
                        PackageInfo = packageInfo,
                        FileCount = entryCount,
                        ArchiveSizeBytes = archiveSize,
                        Message = $"{successMessage}（解析提示: {packageInfo.ErrorMessage}）"
                    };
                }

                return new AppBackupResult
                {
                    Success = true,
                    LocalAmrPath = localAmrPath,
                    PackageInfo = packageInfo,
                    FileCount = entryCount,
                    ArchiveSizeBytes = archiveSize,
                    Message = successMessage
                };
            }
            finally
            {
                try
                {
                    await _adbService.ShellAsync(serial, $"rm -f {quotedZip}").ConfigureAwait(false);
                }
                catch
                {

                }
            }
        }

        public static string GetSystemAppBackupDirectory()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "YoudaoPenToolbox",
                "SystemAppBackups");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string BuildAutoBackupPath(InstalledApp app)
        {
            return Path.Combine(GetSystemAppBackupDirectory(), BuildDefaultFileName(app));
        }

        public static string BuildDefaultFileName(InstalledApp app)
        {
            var appId = SanitizeFileName(app?.AppId ?? "unknown");
            var safeName = SanitizeFileName(app?.Name ?? "app");
            var version = SanitizeFileName(app?.Version ?? "unknown");
            return $"{safeName}_v{version}_{appId}.amr";
        }

        private const string RemoteInstallNameChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string BuildSafeRemoteInstallName()
        {
            var buffer = new char[10];
            var randomBytes = new byte[10];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = RemoteInstallNameChars[randomBytes[i] % RemoteInstallNameChars.Length];
            }

            return new string(buffer) + ".amr";
        }

        public static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value.Trim())
            {
                builder.Append(invalid.Contains(ch) ? '_' : ch);
            }

            var result = builder.ToString().Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
        }

        private async Task<string> ResolveInstallPathAsync(string serial, InstalledApp app)
        {
            var liveInstallPath = await _packageService.GetInstallPathAsync(serial, app.AppId).ConfigureAwait(false);
            var candidates = new[]
            {
                liveInstallPath,
                app.InstallPath,
                RemotePathHelper.Combine(app.PackageDir, "b"),
                RemotePathHelper.Combine(app.PackageDir, "a")
            };

            foreach (var candidate in candidates.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalized = RemotePathHelper.Normalize(candidate);
                if (await DirectoryExistsAsync(serial, normalized).ConfigureAwait(false))
                {
                    var manifestPath = RemotePathHelper.Combine(normalized, "manifest.json");
                    if (await FileExistsAsync(serial, manifestPath).ConfigureAwait(false))
                    {
                        return normalized;
                    }
                }
            }

            throw new InvalidOperationException(
                $"无法定位 [{app.Name}] 的安装目录（缺少 manifest.json）。\n" +
                $"packageDir: {app.PackageDir}\ninstallPath: {liveInstallPath ?? app.InstallPath}\n" +
                "请先在 APP 列表点击「刷新列表」后重试。");
        }

        private async Task<bool> DirectoryExistsAsync(string serial, string path)
        {
            var quoted = RemotePathHelper.ShellQuote(path);
            var output = await _adbService.ShellAsync(serial, $"test -d {quoted} && echo OK || echo NO").ConfigureAwait(false);
            return output != null && output.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<bool> FileExistsAsync(string serial, string path)
        {
            var quoted = RemotePathHelper.ShellQuote(path);
            var output = await _adbService.ShellAsync(serial, $"test -f {quoted} && echo OK || echo NO").ConfigureAwait(false);
            return output != null && output.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureZipSuccess(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException("设备端 zip 打包无响应");
            }

            if (output.IndexOf("ZIP_OK", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var text = output.Trim();
                if (text.Length > 300)
                {
                    text = text.Substring(0, 300) + "...";
                }

                throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? "设备端 zip 打包失败" : text);
            }
        }

        private static long ParseLong(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var token = text.Trim().Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return long.TryParse(token, out var value) ? value : 0;
        }

        private static int ParseZipEntryCount(string tailLine)
        {
            if (string.IsNullOrWhiteSpace(tailLine))
            {
                return 0;
            }

            var parts = tailLine.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1 && int.TryParse(parts[0], out var count))
            {
                return count;
            }

            return 0;
        }
    }
}
