using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public partial class DeviceFileBrowserService
    {
        public async Task CreateDirectoryAsync(string serial, string parentPath, string folderName)
        {
            ValidateEntryName(folderName);
            var targetPath = RemotePathHelper.Combine(parentPath, folderName.Trim());
            var quotedPath = RemotePathHelper.ShellQuote(targetPath);
            var output = await _adbService.ShellAsync(serial, $"mkdir -p {quotedPath} 2>&1").ConfigureAwait(false);
            EnsureShellSuccess(output, "创建文件夹失败");
        }

        public async Task RenameAsync(string serial, RemoteFileItem item, string newName)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            ValidateEntryName(newName);
            var parentPath = RemotePathHelper.GetParent(item.FullPath);
            var targetPath = RemotePathHelper.Combine(parentPath, newName.Trim());
            if (string.Equals(item.FullPath, targetPath, StringComparison.Ordinal))
            {
                return;
            }

            var oldQuoted = RemotePathHelper.ShellQuote(item.FullPath);
            var newQuoted = RemotePathHelper.ShellQuote(targetPath);
            var output = await _adbService.ShellAsync(serial, $"mv {oldQuoted} {newQuoted} 2>&1").ConfigureAwait(false);
            EnsureShellSuccess(output, "重命名失败");
        }

        public async Task DeleteManyAsync(string serial, IEnumerable<RemoteFileItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item != null)
                {
                    await DeleteAsync(serial, item).ConfigureAwait(false);
                }
            }
        }

        public static void ValidateEntryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("名称不能为空");
            }

            var trimmed = name.Trim();
            if (trimmed == "." || trimmed == "..")
            {
                throw new InvalidOperationException("名称无效");
            }

            if (trimmed.IndexOf('/') >= 0 || trimmed.IndexOf('\\') >= 0)
            {
                throw new InvalidOperationException("名称不能包含路径分隔符");
            }
        }

        private static void EnsureShellSuccess(string output, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            var text = output.Trim();
            if (text.IndexOf("No such file", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Read-only", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Not a directory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(text) ? fallbackMessage : text);
            }
        }
    }
}
