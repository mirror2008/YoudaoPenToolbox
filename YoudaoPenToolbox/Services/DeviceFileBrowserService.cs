using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class RemoteDirectoryListing
    {
        public IReadOnlyList<RemoteFileItem> Items { get; set; } = Array.Empty<RemoteFileItem>();
        public string ErrorMessage { get; set; }
    }

    public static class RemotePathHelper
    {
        /// <summary>有道笔用户可写数据盘，文件管理器默认打开此目录。</summary>
        public const string DefaultUserDataPath = "/userdisk";

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            var normalized = path.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            if (normalized.Length > 1)
            {
                normalized = normalized.TrimEnd('/');
            }

            return string.IsNullOrEmpty(normalized) ? "/" : normalized;
        }

        public static string GetParent(string path)
        {
            var normalized = Normalize(path);
            if (normalized == "/")
            {
                return "/";
            }

            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash <= 0 ? "/" : normalized.Substring(0, lastSlash);
        }

        public static string Combine(string directory, string name)
        {
            var dir = Normalize(directory);
            var entryName = name?.Trim().TrimStart('/') ?? string.Empty;
            if (string.IsNullOrEmpty(entryName))
            {
                return dir;
            }

            return dir == "/" ? "/" + entryName : dir + "/" + entryName;
        }

        public static string ShellQuote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "''";
            }

            return "'" + value.Replace("'", "'\\''") + "'";
        }
    }

    public partial class DeviceFileBrowserService
    {
        private static readonly Regex LsLineRegex = new Regex(
            @"^(?<perm>[dlrwxSt-]+)\s+\d+\s+\S+\s+\S+\s+(?<size>\d+)\s+(?<date>\S+\s+\d+\s+(?:\d{4}|\d{1,2}:\d{2}))\s+(?<name>.+)$",
            RegexOptions.Compiled);

        private readonly AdbService _adbService;

        public DeviceFileBrowserService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public async Task<RemoteDirectoryListing> ListDirectoryAsync(string serial, string path)
        {
            var normalizedPath = RemotePathHelper.Normalize(path);
            var quotedPath = RemotePathHelper.ShellQuote(normalizedPath);
            var output = await _adbService.ShellAsync(serial, $"ls -la {quotedPath} 2>&1").ConfigureAwait(false);
            return ParseListing(output, normalizedPath);
        }

        public async Task<bool> DirectoryExistsAsync(string serial, string path)
        {
            return await IsDirectoryPathAsync(serial, path).ConfigureAwait(false);
        }

        public async Task<bool> EntryExistsAsync(string serial, string path)
        {
            var normalized = RemotePathHelper.Normalize(path);
            var quotedPath = RemotePathHelper.ShellQuote(normalized);
            var lsOutput = await _adbService.ShellAsync(serial, $"ls -ld {quotedPath} 2>&1").ConfigureAwait(false);
            return !IsLsAccessError(lsOutput);
        }

        public async Task<bool> FileExistsAtPathAsync(string serial, string path)
        {
            var normalized = RemotePathHelper.Normalize(path);
            var quotedPath = RemotePathHelper.ShellQuote(normalized);
            var lsOutput = await _adbService.ShellAsync(serial, $"ls -ld {quotedPath} 2>&1").ConfigureAwait(false);
            if (IsLsAccessError(lsOutput))
            {
                return false;
            }

            return GetLsPermissionMarker(lsOutput) == '-';
        }

        private async Task<bool> IsDirectoryPathAsync(string serial, string path)
        {
            var normalized = RemotePathHelper.Normalize(path);
            var quotedPath = RemotePathHelper.ShellQuote(normalized);
            var lsOutput = await _adbService.ShellAsync(serial, $"ls -ld {quotedPath} 2>&1").ConfigureAwait(false);
            if (!IsLsAccessError(lsOutput))
            {
                return GetLsPermissionMarker(lsOutput) == 'd';
            }

            var testOutput = await _adbService.ShellScriptAsync(serial, $"test -d {quotedPath} && echo __DIR_OK__")
                .ConfigureAwait(false);
            return testOutput != null && testOutput.IndexOf("__DIR_OK__", StringComparison.Ordinal) >= 0;
        }

        private static char? GetLsPermissionMarker(string lsOutput)
        {
            if (string.IsNullOrWhiteSpace(lsOutput))
            {
                return null;
            }

            var line = lsOutput.Trim();
            var spaceIndex = line.IndexOf(' ');
            var perm = spaceIndex > 0 ? line.Substring(0, spaceIndex) : line;
            return perm.Length > 0 ? perm[0] : (char?)null;
        }

        private async Task<bool> ListingContainsDirectoryAsync(string serial, string parentPath, string entryName)
        {
            var listing = await ListDirectoryAsync(serial, parentPath).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(listing.ErrorMessage))
            {
                return false;
            }

            foreach (var item in listing.Items)
            {
                if (!string.Equals(GetEntryFileName(item), entryName, StringComparison.Ordinal))
                {
                    continue;
                }

                return item.IsDirectory || item.IsSymlink;
            }

            return false;
        }

        private async Task EnsureParentAccessibleAsync(string serial, string parentPath)
        {
            var listing = await ListDirectoryAsync(serial, parentPath).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(listing.ErrorMessage))
            {
                var detail = listing.ErrorMessage.Trim();
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detail)
                        ? $"父目录不存在或无法访问: {parentPath}"
                        : $"父目录不存在或无法访问: {parentPath}\n{detail}");
            }
        }

        private static bool IsLsAccessError(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return true;
            }

            return output.IndexOf("No such file", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("cannot access", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task<bool> IsDirectoryWritableAsync(string serial, string directoryPath)
        {
            var parent = RemotePathHelper.Normalize(directoryPath);
            var probeName = $".ypt_write_probe_{Guid.NewGuid():N}";
            var probePath = RemotePathHelper.Combine(parent, probeName);
            var quotedProbe = RemotePathHelper.ShellQuote(probePath);

            var touchOutput = await _adbService.ShellAsync(serial, $"touch {quotedProbe} 2>&1").ConfigureAwait(false);
            if (IsWriteDeniedOutput(touchOutput))
            {
                return false;
            }

            if (await EntryExistsAsync(serial, probePath).ConfigureAwait(false))
            {
                await _adbService.ShellAsync(serial, $"rm -f {quotedProbe} 2>&1").ConfigureAwait(false);
                return true;
            }

            var mkdirOutput = await _adbService.ShellAsync(serial, $"mkdir {quotedProbe} 2>&1").ConfigureAwait(false);
            if (IsWriteDeniedOutput(mkdirOutput))
            {
                return false;
            }

            if (!await DirectoryExistsAsync(serial, probePath).ConfigureAwait(false))
            {
                return false;
            }

            await _adbService.ShellAsync(serial, $"rmdir {quotedProbe} 2>&1").ConfigureAwait(false);
            return true;
        }

        private static bool IsWriteDeniedOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            return output.IndexOf("Read-only file system", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("Read-only", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task DeleteAsync(string serial, RemoteFileItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var quotedPath = RemotePathHelper.ShellQuote(item.FullPath);
            var command = item.IsDirectory && !item.IsSymlink
                ? $"rm -rf {quotedPath}"
                : $"rm -f {quotedPath}";
            await _adbService.ShellAsync(serial, command).ConfigureAwait(false);
        }

        public async Task<bool> UploadFileAsync(string serial, string localPath, string remoteDirectory)
        {
            var fileName = System.IO.Path.GetFileName(localPath);
            var remotePath = RemotePathHelper.Combine(remoteDirectory, fileName);
            return await _adbService.PushFileAsync(serial, localPath, remotePath).ConfigureAwait(false);
        }

        public static RemoteDirectoryListing ParseListing(string output, string currentPath)
        {
            var listing = new RemoteDirectoryListing();
            if (string.IsNullOrWhiteSpace(output))
            {
                listing.ErrorMessage = "目录列表为空";
                return listing;
            }

            var items = new List<RemoteFileItem>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (line.StartsWith("total ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("ls:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var item = ParseLine(line, currentPath);
                if (item == null || item.Name == "." || item.Name == "..")
                {
                    continue;
                }

                items.Add(item);
            }

            if (items.Count == 0 && LooksLikeListingFailure(output))
            {
                listing.ErrorMessage = output.Trim();
                return listing;
            }

            listing.Items = items
                .OrderByDescending(i => i.IsDirectory)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return listing;
        }

        private static bool LooksLikeListingFailure(string output)
        {
            return output.IndexOf("No such file", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("cannot access", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static RemoteFileItem ParseLine(string line, string currentPath)
        {
            var match = LsLineRegex.Match(line);
            if (!match.Success)
            {
                return ParseLineFallback(line, currentPath);
            }

            var permissions = match.Groups["perm"].Value;
            var isSymlink = permissions.StartsWith("l", StringComparison.Ordinal);
            var isDirectory = permissions.StartsWith("d", StringComparison.Ordinal);
            var rawName = match.Groups["name"].Value.Trim();
            var entryName = rawName;
            string symlinkTarget = null;

            if (isSymlink)
            {
                var arrowIndex = rawName.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrowIndex >= 0)
                {
                    entryName = rawName.Substring(0, arrowIndex).Trim();
                    symlinkTarget = rawName.Substring(arrowIndex + 4).Trim();
                }
            }

            if (!long.TryParse(match.Groups["size"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeBytes))
            {
                sizeBytes = 0;
            }

            return new RemoteFileItem
            {
                Name = rawName,
                FullPath = RemotePathHelper.Combine(currentPath, entryName),
                IsDirectory = isDirectory,
                IsSymlink = isSymlink,
                SizeBytes = sizeBytes,
                SizeDisplay = FormatSize(isDirectory, isSymlink, sizeBytes),
                Permissions = permissions,
                ModifiedDisplay = match.Groups["date"].Value.Trim(),
                SymlinkTarget = symlinkTarget
            };
        }

        private static RemoteFileItem ParseLineFallback(string line, string currentPath)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("ls:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var trimmed = line.Trim();
            var arrowIndex = trimmed.IndexOf(" -> ", StringComparison.Ordinal);
            var entryName = arrowIndex >= 0 ? trimmed.Substring(0, arrowIndex).Trim() : trimmed;
            if (entryName == "." || entryName == "..")
            {
                return null;
            }

            var isSymlink = arrowIndex >= 0;
            var isDirectory = trimmed.StartsWith("d", StringComparison.Ordinal) || isSymlink;

            return new RemoteFileItem
            {
                Name = entryName,
                FullPath = RemotePathHelper.Combine(currentPath, entryName),
                IsDirectory = isDirectory,
                IsSymlink = isSymlink,
                SizeBytes = 0,
                SizeDisplay = FormatSize(isDirectory, isSymlink, 0),
                Permissions = isDirectory ? "d?????????" : "-?????????",
                ModifiedDisplay = string.Empty,
                SymlinkTarget = isSymlink ? trimmed.Substring(arrowIndex + 4).Trim() : null
            };
        }

        private static string FormatSize(bool isDirectory, bool isSymlink, long sizeBytes)
        {
            if (isDirectory)
            {
                return "<DIR>";
            }

            if (isSymlink)
            {
                return "链接";
            }

            if (sizeBytes >= 1024 * 1024 * 1024)
            {
                return $"{sizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
            }

            if (sizeBytes >= 1024 * 1024)
            {
                return $"{sizeBytes / 1024.0 / 1024.0:F2} MB";
            }

            if (sizeBytes >= 1024)
            {
                return $"{sizeBytes / 1024.0:F1} KB";
            }

            return $"{sizeBytes} B";
        }
    }
}
