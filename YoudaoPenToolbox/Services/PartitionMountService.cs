using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class PartitionMountResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string MountPoint { get; set; }
        public string FilesystemType { get; set; }
    }

    public class PartitionMountService
    {
        private static readonly Regex FstabLineRegex = new Regex(
            @"^\s*(/\S+|UUID=[^\s]+|LABEL=[^\s]+)\s+(\S+)\s+(\S+)",
            RegexOptions.Compiled);

        private static readonly Regex BlkidTypeRegex = new Regex(
            @"TYPE=""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, string> KnownMountPoints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userdata"] = "/userdata",
                ["userdisk"] = "/userdisk",
                ["system"] = "/system",
                ["cache"] = "/cache",
                ["misc"] = "/misc"
            };

        private static readonly string[] FilesystemProbeOrder = { "ext4", "f2fs", "vfat", "ext2", "exfat", "ntfs" };

        private readonly AdbService _adbService;

        public PartitionMountService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public static string GetSuggestedMountPoint(string partitionName)
        {
            var baseName = StripAbSlotSuffix(partitionName);
            if (KnownMountPoints.TryGetValue(baseName, out var mountPoint))
            {
                return mountPoint;
            }

            return $"/mnt/ypt/{SanitizeMountName(partitionName)}";
        }

        public static bool IsWholeDiskPartition(BlockPartitionInfo partition)
        {
            if (partition == null)
            {
                return true;
            }

            return string.Equals(partition.BlockDeviceName, "mmcblk0", StringComparison.OrdinalIgnoreCase)
                || (partition.Name?.IndexOf("整盘", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        public async Task<string> ResolveMountPointAsync(string serial, BlockPartitionInfo partition)
        {
            var fstabPoint = await LookupFstabMountPointAsync(serial, partition).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fstabPoint))
            {
                return fstabPoint;
            }

            var suggested = GetSuggestedMountPoint(partition.Name);
            var mountMap = ParseProcMounts(await _adbService.ShellAsync(serial, "cat /proc/mounts 2>/dev/null").ConfigureAwait(false));

            if (!mountMap.ContainsKey(NormalizeMountKey(suggested)))
            {
                return suggested;
            }

            if (mountMap.TryGetValue(NormalizeMountKey(suggested), out var mountedDevice)
                && string.Equals(
                    mountedDevice,
                    partition.BlockDeviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return suggested;
            }

            return $"/mnt/ypt/{SanitizeMountName(partition.Name)}";
        }

        public async Task<string> DetectFilesystemTypeAsync(string serial, BlockPartitionInfo partition)
        {
            var device = ResolveMountDevice(partition);
            var output = await _adbService.ShellAsync(
                serial,
                $"blkid -o value -s TYPE {Quote(device)} 2>/dev/null; " +
                $"blkid {Quote(device)} 2>/dev/null; " +
                $"file -sL {Quote(device)} 2>/dev/null | head -1")
                .ConfigureAwait(false);

            return ParseFilesystemType(output);
        }

        public async Task<PartitionMountResult> MountPartitionAsync(
            string serial,
            BlockPartitionInfo partition,
            string mountPoint,
            string filesystemType = null,
            bool readOnly = false)
        {
            if (partition == null)
            {
                return Fail("未选择分区");
            }

            if (partition.IsMounted)
            {
                return Fail($"分区已挂载于 {partition.MountPoint}");
            }

            if (IsWholeDiskPartition(partition))
            {
                return Fail("不支持挂载整盘 mmcblk0");
            }

            mountPoint = NormalizeMountPoint(mountPoint);
            if (mountPoint == null)
            {
                return Fail("挂载点无效，请使用以 / 开头的绝对路径");
            }

            var device = ResolveMountDevice(partition);
            var deviceCheck = await _adbService.ShellAsync(
                serial,
                $"test -b {Quote(device)} && echo __BLOCK_OK__ || echo __BLOCK_MISSING__")
                .ConfigureAwait(false);
            if (deviceCheck?.IndexOf("__BLOCK_OK__", StringComparison.Ordinal) < 0)
            {
                return Fail($"块设备不存在或不可访问: {device}");
            }

            var mountOptions = readOnly ? "ro" : "rw";
            await _adbService.ShellAsync(serial, $"mkdir -p {Quote(mountPoint)}").ConfigureAwait(false);

            var attempts = BuildMountAttempts(device, mountPoint, mountOptions, filesystemType);
            if (string.IsNullOrWhiteSpace(filesystemType))
            {
                var detected = await DetectFilesystemTypeAsync(serial, partition).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(detected))
                {
                    attempts.Insert(0, BuildMountCommand(detected, device, mountPoint, mountOptions));
                }
            }

            string lastError = null;
            foreach (var command in attempts.Distinct(StringComparer.Ordinal))
            {
                var output = await _adbService.ShellAsync(serial, $"{command} 2>&1").ConfigureAwait(false);
                if (await IsMountPointActiveAsync(serial, mountPoint, partition).ConfigureAwait(false))
                {
                    return new PartitionMountResult
                    {
                        Success = true,
                        MountPoint = mountPoint,
                        FilesystemType = filesystemType ?? ParseFilesystemType(output),
                        Message = $"已挂载到 {mountPoint}"
                    };
                }

                lastError = string.IsNullOrWhiteSpace(output) ? "mount 命令失败" : output.Trim();
            }

            return Fail(string.IsNullOrWhiteSpace(lastError) ? "挂载失败，请检查文件系统类型或权限" : lastError);
        }

        public async Task<PartitionMountResult> UnmountPartitionAsync(
            string serial,
            BlockPartitionInfo partition,
            bool lazy = false)
        {
            if (partition == null || !partition.IsMounted || string.IsNullOrWhiteSpace(partition.MountPoint))
            {
                return Fail("该分区当前未挂载");
            }

            var mountPoint = partition.MountPoint;
            var flag = lazy ? "-l" : string.Empty;
            var output = await _adbService.ShellAsync(
                serial,
                $"umount {flag} {Quote(mountPoint)} 2>&1")
                .ConfigureAwait(false);

            var stillMounted = await IsMountPointActiveAsync(serial, mountPoint, partition).ConfigureAwait(false);
            if (stillMounted)
            {
                return Fail(string.IsNullOrWhiteSpace(output) ? "卸载失败，分区可能仍被占用" : output.Trim());
            }

            return new PartitionMountResult
            {
                Success = true,
                MountPoint = mountPoint,
                Message = $"已从 {mountPoint} 卸载"
            };
        }

        private async Task<bool> IsMountPointActiveAsync(string serial, string mountPoint, BlockPartitionInfo partition)
        {
            var mounts = ParseProcMountsDetailed(
                await _adbService.ShellAsync(serial, "cat /proc/mounts 2>/dev/null").ConfigureAwait(false));

            return mounts.Any(entry =>
                string.Equals(entry.MountPoint, mountPoint, StringComparison.Ordinal)
                && string.Equals(entry.BlockName, partition.BlockDeviceName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<string> LookupFstabMountPointAsync(string serial, BlockPartitionInfo partition)
        {
            var fstab = await _adbService.ShellAsync(serial, "cat /etc/fstab 2>/dev/null").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fstab))
            {
                return null;
            }

            foreach (var line in fstab.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = FstabLineRegex.Match(trimmed);
                if (!match.Success)
                {
                    continue;
                }

                var deviceSpec = match.Groups[1].Value;
                var mountPoint = match.Groups[2].Value;
                if (string.Equals(deviceSpec, partition.BlockDevicePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(deviceSpec, ResolveMountDevice(partition), StringComparison.OrdinalIgnoreCase))
                {
                    return mountPoint;
                }
            }

            return null;
        }

        private static List<string> BuildMountAttempts(
            string device,
            string mountPoint,
            string mountOptions,
            string filesystemType)
        {
            var attempts = new List<string>();
            if (!string.IsNullOrWhiteSpace(filesystemType))
            {
                attempts.Add(BuildMountCommand(filesystemType, device, mountPoint, mountOptions));
            }

            foreach (var fs in FilesystemProbeOrder)
            {
                attempts.Add(BuildMountCommand(fs, device, mountPoint, mountOptions));
            }

            attempts.Add($"mount -o {mountOptions} {Quote(device)} {Quote(mountPoint)}");
            return attempts;
        }

        private static string BuildMountCommand(string filesystemType, string device, string mountPoint, string mountOptions)
        {
            return $"mount -t {filesystemType} -o {mountOptions} {Quote(device)} {Quote(mountPoint)}";
        }

        private static string ResolveMountDevice(BlockPartitionInfo partition)
        {
            if (!string.IsNullOrWhiteSpace(partition.ByNamePath)
                && partition.ByNamePath.IndexOf("/by-name/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return partition.ByNamePath;
            }

            return partition.BlockDevicePath;
        }

        private static string ParseFilesystemType(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (FilesystemProbeOrder.Any(fs => string.Equals(trimmed, fs, StringComparison.OrdinalIgnoreCase)))
                {
                    return trimmed.ToLowerInvariant();
                }

                var match = BlkidTypeRegex.Match(trimmed);
                if (match.Success)
                {
                    return match.Groups[1].Value.ToLowerInvariant();
                }

                var lower = trimmed.ToLowerInvariant();
                foreach (var fs in FilesystemProbeOrder)
                {
                    if (lower.Contains(fs))
                    {
                        return fs;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<(string BlockName, string MountPoint)> ParseProcMountsDetailed(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                yield break;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !parts[0].StartsWith("/dev/", StringComparison.Ordinal))
                {
                    continue;
                }

                var blockName = parts[0].Substring(parts[0].LastIndexOf('/') + 1);
                yield return (blockName, parts[1]);
            }
        }

        private static Dictionary<string, string> ParseProcMounts(string output)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ParseProcMountsDetailed(output))
            {
                map[NormalizeMountKey(entry.MountPoint)] = entry.BlockName;
            }

            return map;
        }

        private static string NormalizeMountKey(string mountPoint)
        {
            return mountPoint?.TrimEnd('/') ?? string.Empty;
        }

        private static string NormalizeMountPoint(string mountPoint)
        {
            if (string.IsNullOrWhiteSpace(mountPoint))
            {
                return null;
            }

            mountPoint = mountPoint.Trim();
            if (!mountPoint.StartsWith("/", StringComparison.Ordinal))
            {
                return null;
            }

            return mountPoint.TrimEnd('/');
        }

        private static string StripAbSlotSuffix(string partitionName)
        {
            if (string.IsNullOrWhiteSpace(partitionName))
            {
                return string.Empty;
            }

            var match = Regex.Match(partitionName, @"^(.+)_[ab]$", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : partitionName;
        }

        public static string SanitizeMountName(string partitionName)
        {
            if (string.IsNullOrWhiteSpace(partitionName))
            {
                return "partition";
            }

            var chars = partitionName.Select(ch =>
                char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray();
            return new string(chars);
        }

        private static string Quote(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\\''") + "'";
        }

        private static PartitionMountResult Fail(string message)
        {
            return new PartitionMountResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
