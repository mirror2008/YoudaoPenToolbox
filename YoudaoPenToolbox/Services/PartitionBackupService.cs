using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public static class PartitionBackupService
    {
        public static readonly string[] PresetBaseNames = { "boot", "system", "trust", "userdata" };

        public static string GetPartitionBackupDirectory(string serial)
        {
            var safeSerial = SanitizeFileName(string.IsNullOrWhiteSpace(serial) ? "unknown" : serial);
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "YoudaoPenToolbox",
                "PartitionBackups",
                safeSerial);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string BuildDefaultExtractPath(string serial, string partitionName, DateTime? timestamp = null)
        {
            var stamp = (timestamp ?? DateTime.Now).ToString("yyyyMMdd_HHmmss");
            var safeName = SanitizeFileName(partitionName ?? "partition");
            return Path.Combine(GetPartitionBackupDirectory(serial), $"{safeName}_{stamp}.img");
        }

        public static string BuildBatchBackupDirectory(string serial, DateTime? timestamp = null)
        {
            var stamp = (timestamp ?? DateTime.Now).ToString("yyyyMMdd_HHmmss");
            var dir = Path.Combine(GetPartitionBackupDirectory(serial), $"batch_{stamp}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static IReadOnlyList<BlockPartitionInfo> ResolvePresetPartitions(
            IEnumerable<BlockPartitionInfo> partitions,
            string activeAbSlot)
        {
            var list = partitions?.Where(p => p != null).ToList() ?? new List<BlockPartitionInfo>();
            var selected = new List<BlockPartitionInfo>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var baseName in PresetBaseNames)
            {
                var match = FindPresetPartition(list, baseName, activeAbSlot);
                if (match != null && used.Add(match.BlockDeviceName ?? match.Name))
                {
                    selected.Add(match);
                }
            }

            return selected;
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

            return builder.ToString();
        }

        private static BlockPartitionInfo FindPresetPartition(
            IReadOnlyList<BlockPartitionInfo> partitions,
            string baseName,
            string activeAbSlot)
        {
            var exact = partitions.FirstOrDefault(p =>
                string.Equals(p.Name, baseName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            if (!string.IsNullOrWhiteSpace(activeAbSlot))
            {
                var slotted = partitions.FirstOrDefault(p =>
                    string.Equals(p.Name, $"{baseName}_{activeAbSlot}", StringComparison.OrdinalIgnoreCase));
                if (slotted != null)
                {
                    return slotted;
                }
            }

            return partitions.FirstOrDefault(p =>
                p.Name != null
                && p.Name.StartsWith(baseName + "_", StringComparison.OrdinalIgnoreCase));
        }
    }
}
