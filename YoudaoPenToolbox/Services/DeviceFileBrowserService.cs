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
            var quotedPath = RemotePathHelper.ShellQuote(RemotePathHelper.Normalize(path));
            var output = await _adbService.ShellAsync(serial, $"test -d {quotedPath} && echo OK || echo NO").ConfigureAwait(false);
            return output != null && output.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0;
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

            if (output.IndexOf("No such file", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("cannot access", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                listing.ErrorMessage = output.Trim();
                return listing;
            }

            var items = new List<RemoteFileItem>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (line.StartsWith("total ", StringComparison.OrdinalIgnoreCase))
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

            listing.Items = items
                .OrderByDescending(i => i.IsDirectory)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return listing;
        }

        private static RemoteFileItem ParseLine(string line, string currentPath)
        {
            var match = LsLineRegex.Match(line);
            if (!match.Success)
            {
                return null;
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
