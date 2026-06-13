using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class PartitionService
    {
        private static readonly Regex ByNameLinkRegex = new Regex(
            @"(\S+)\s+->\s+(/dev/\S+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex ProcPartitionRegex = new Regex(
            @"^\s*(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex ProcMountDeviceRegex = new Regex(
            @"^(/dev/\S+)\s+(\S+)\s+",
            RegexOptions.Compiled);

        private static readonly Regex CmdlineSlotRegex = new Regex(
            @"(?:androidboot\.)?slot_suffix=_([ab])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AbSlotPartitionRegex = new Regex(
            @"^(.+)_(a|b)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly AdbService _adbService;

        public PartitionService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public async Task<PartitionListResult> GetPartitionsAsync(string serial)
        {
            var sizeMap = ParseProcPartitions(await _adbService.ShellAsync(serial, "cat /proc/partitions").ConfigureAwait(false));
            var mountMap = ParseProcMounts(await _adbService.ShellAsync(serial, "cat /proc/mounts 2>/dev/null").ConfigureAwait(false));
            var activeSlot = await GetActiveAbSlotAsync(serial).ConfigureAwait(false);
            var byBlockName = new Dictionary<string, BlockPartitionInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in new[] { "/dev/block/by-name/", "/dev/by-name/" })
            {
                var output = await _adbService.ShellAsync(serial, $"ls -l {path} 2>/dev/null").ConfigureAwait(false);
                ParseByNameListing(output, path.TrimEnd('/'), sizeMap, byBlockName);
            }

            foreach (var entry in sizeMap.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (!entry.Key.StartsWith("mmcblk0p", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(entry.Key, "mmcblk0", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (byBlockName.ContainsKey(entry.Key))
                {
                    continue;
                }

                byBlockName[entry.Key] = CreatePartition(
                    entry.Key,
                    $"/dev/{entry.Key}",
                    $"/dev/{entry.Key}",
                    entry.Key,
                    entry.Value);
            }

            if (sizeMap.TryGetValue("mmcblk0", out var diskSize)
                && !byBlockName.ContainsKey("mmcblk0"))
            {
                byBlockName["mmcblk0"] = CreatePartition(
                    "mmcblk0 (整盘)",
                    "/dev/mmcblk0",
                    "/dev/mmcblk0",
                    "mmcblk0",
                    diskSize,
                    critical: true);
            }

            foreach (var partition in byBlockName.Values)
            {
                ApplyMountInfo(partition, mountMap);
                ApplyAbSlotInfo(partition, activeSlot);
            }

            return new PartitionListResult
            {
                ActiveAbSlot = activeSlot,
                Partitions = byBlockName.Values
                    .OrderBy(p => GetSortKey(p))
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static BlockPartitionInfo CreatePartition(
            string name,
            string byNamePath,
            string blockDevicePath,
            string blockDeviceName,
            long sizeBytes,
            bool? critical = null)
        {
            return new BlockPartitionInfo
            {
                Name = name,
                ByNamePath = byNamePath,
                BlockDevicePath = blockDevicePath,
                BlockDeviceName = blockDeviceName,
                SizeBytes = sizeBytes,
                IsCritical = critical ?? BlockPartitionInfo.IsCriticalPartitionName(name)
            };
        }

        private static void ApplyMountInfo(BlockPartitionInfo partition, Dictionary<string, string> mountMap)
        {
            if (string.IsNullOrWhiteSpace(partition.BlockDeviceName))
            {
                return;
            }

            if (mountMap.TryGetValue(partition.BlockDeviceName, out var mountPoint))
            {
                partition.IsMounted = true;
                partition.MountPoint = mountPoint;
            }
        }

        private static void ApplyAbSlotInfo(BlockPartitionInfo partition, string activeSlot)
        {
            var match = AbSlotPartitionRegex.Match(partition.Name ?? string.Empty);
            if (!match.Success)
            {
                return;
            }

            partition.AbSlotLetter = match.Groups[2].Value.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(activeSlot))
            {
                partition.IsActiveAbSlot = string.Equals(
                    partition.AbSlotLetter,
                    activeSlot,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private async Task<string> GetActiveAbSlotAsync(string serial)
        {
            var cmdline = await _adbService.ShellAsync(serial, "cat /proc/cmdline 2>/dev/null").ConfigureAwait(false);
            var match = CmdlineSlotRegex.Match(cmdline ?? string.Empty);
            if (match.Success)
            {
                return match.Groups[1].Value.ToLowerInvariant();
            }

            var abctl = await _adbService.ShellAsync(
                serial,
                "abctl --boot_slot 2>/dev/null; abctl get_slot 2>/dev/null; getprop ro.boot.slot_suffix 2>/dev/null")
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(abctl))
            {
                foreach (var line in abctl.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim().TrimStart('_').ToLowerInvariant();
                    if (trimmed == "a" || trimmed == "b")
                    {
                        return trimmed;
                    }

                    var slotMatch = CmdlineSlotRegex.Match(line);
                    if (slotMatch.Success)
                    {
                        return slotMatch.Groups[1].Value.ToLowerInvariant();
                    }
                }
            }

            return null;
        }

        private static void ParseByNameListing(
            string output,
            string byNameBase,
            Dictionary<string, long> sizeMap,
            Dictionary<string, BlockPartitionInfo> target)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("total", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var match = ByNameLinkRegex.Match(trimmed);
                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups[1].Value;
                var blockPath = match.Groups[2].Value;
                var blockName = blockPath.Substring(blockPath.LastIndexOf('/') + 1);
                sizeMap.TryGetValue(blockName, out var sizeBytes);

                target[blockName] = CreatePartition(
                    name,
                    $"{byNameBase}/{name}",
                    blockPath,
                    blockName,
                    sizeBytes);
            }
        }

        private static Dictionary<string, long> ParseProcPartitions(string output)
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(output))
            {
                return map;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = ProcPartitionRegex.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                if (!long.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var blocks))
                {
                    continue;
                }

                map[match.Groups[4].Value] = blocks * 512L;
            }

            return map;
        }

        private static Dictionary<string, string> ParseProcMounts(string output)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(output))
            {
                return map;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = ProcMountDeviceRegex.Match(line.Trim());
                if (!match.Success)
                {
                    continue;
                }

                var devicePath = match.Groups[1].Value;
                var mountPoint = match.Groups[2].Value;
                if (!devicePath.StartsWith("/dev/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var blockName = devicePath.Substring(devicePath.LastIndexOf('/') + 1);
                map[blockName] = mountPoint;
            }

            return map;
        }

        private static int GetSortKey(BlockPartitionInfo partition)
        {
            if (string.Equals(partition.BlockDeviceName, "mmcblk0", StringComparison.OrdinalIgnoreCase))
            {
                return 10000;
            }

            var match = Regex.Match(partition.BlockDeviceName ?? string.Empty, @"p(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
            {
                return index;
            }

            return 5000;
        }
    }
}
