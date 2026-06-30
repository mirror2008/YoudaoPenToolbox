using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public partial class DeviceFileBrowserService
    {
        public async Task CreateDirectoryAsync(string serial, string parentPath, string folderName)
        {
            ValidateEntryName(folderName);

            var parent = RemotePathHelper.Normalize(parentPath);
            var name = folderName.Trim();
            var targetPath = RemotePathHelper.Combine(parent, name);
            var quotedParent = RemotePathHelper.ShellQuote(parent);
            var quotedName = RemotePathHelper.ShellQuote(name);
            var quotedTarget = RemotePathHelper.ShellQuote(targetPath);

            if (await ListingContainsDirectoryAsync(serial, parent, name).ConfigureAwait(false))
            {
                return;
            }

            if (await FileExistsAtPathAsync(serial, targetPath).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"已存在同名文件「{name}」，请换一个文件夹名称");
            }

            await EnsureParentAccessibleAsync(serial, parent).ConfigureAwait(false);

            if (!await IsDirectoryWritableAsync(serial, parent).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"「{parent}」为只读目录，无法创建文件夹。\n" +
                    $"请切换到 {RemotePathHelper.DefaultUserDataPath}（用户数据盘）后再试。");
            }

            string lastOutput = null;
            string readOnlyHint = null;
            var attempts = new (bool useScript, string command)[]
            {
                (false, $"mkdir -p {quotedTarget} 2>&1"),
                (false, $"busybox mkdir -p {quotedTarget} 2>&1"),
                (false, $"mkdir {quotedTarget} 2>&1"),
                (true, $"mkdir -p {quotedTarget}"),
                (true, $"cd {quotedParent} && mkdir {quotedName}")
            };

            foreach (var attempt in attempts)
            {
                lastOutput = attempt.useScript
                    ? await _adbService.ShellScriptAsync(serial, attempt.command).ConfigureAwait(false)
                    : await _adbService.ShellAsync(serial, attempt.command).ConfigureAwait(false);

                if (IsWriteDeniedOutput(lastOutput))
                {
                    readOnlyHint = lastOutput.Trim();
                }

                if (await ListingContainsDirectoryAsync(serial, parent, name).ConfigureAwait(false))
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(readOnlyHint))
            {
                throw new InvalidOperationException(
                    $"「{parent}」为只读目录，无法创建文件夹。\n" +
                    $"请切换到 {RemotePathHelper.DefaultUserDataPath}（用户数据盘）后再试。\n{readOnlyHint}");
            }

            if (await IsDirectoryPathAsync(serial, targetPath).ConfigureAwait(false)
                && !await ListingContainsDirectoryAsync(serial, parent, name).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"文件夹已在设备上创建，但当前目录列表未显示：{targetPath}\n请点击「刷新」查看。");
            }

            var detail = string.IsNullOrWhiteSpace(lastOutput) ? null : lastOutput.Trim();
            throw new InvalidOperationException(
                detail == null
                    ? $"创建文件夹失败: {targetPath}"
                    : $"创建文件夹失败: {targetPath}\n{detail}");
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

            if (await EntryExistsAsync(serial, targetPath).ConfigureAwait(false))
            {
                throw new InvalidOperationException("目标名称已存在");
            }

            var oldQuoted = RemotePathHelper.ShellQuote(item.FullPath);
            var newQuoted = RemotePathHelper.ShellQuote(targetPath);
            var output = await _adbService.ShellAsync(serial, $"mv {oldQuoted} {newQuoted} 2>&1").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(output))
            {
                output = await _adbService.ShellScriptAsync(serial, $"mv {oldQuoted} {newQuoted}").ConfigureAwait(false);
            }

            if (!await EntryExistsAsync(serial, targetPath).ConfigureAwait(false))
            {
                var message = string.IsNullOrWhiteSpace(output) ? "重命名失败" : output.Trim();
                throw new InvalidOperationException(message);
            }
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
    }
}
