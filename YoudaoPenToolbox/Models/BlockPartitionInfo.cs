using YoudaoPenToolbox.Helpers;

namespace YoudaoPenToolbox.Models
{
    public class BlockPartitionInfo : ViewModelBase
    {
        private bool _isSelectedForBatch;

        public string Name { get; set; }
        public string ByNamePath { get; set; }
        public string BlockDevicePath { get; set; }
        public string BlockDeviceName { get; set; }
        public long SizeBytes { get; set; }
        public bool IsCritical { get; set; }
        public bool IsMounted { get; set; }
        public string MountPoint { get; set; }
        public string AbSlotLetter { get; set; }
        public bool IsActiveAbSlot { get; set; }
        public string SuggestedMountPoint { get; set; }
        public string DetectedFilesystem { get; set; }

        public bool IsSelectedForBatch
        {
            get => _isSelectedForBatch;
            set => SetProperty(ref _isSelectedForBatch, value);
        }

        public string RiskDisplay => IsCritical ? "高" : "普通";
        public string SizeDisplay => FormatSize(SizeBytes);

        public string MountDisplay => IsMounted
            ? $"是 @ {MountPoint ?? "?"}"
            : "否";

        public string SlotDisplay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AbSlotLetter))
                {
                    return "-";
                }

                var label = AbSlotLetter.Equals("a", System.StringComparison.OrdinalIgnoreCase) ? "A" : "B";
                return IsActiveAbSlot ? $"{label}·当前" : label;
            }
        }

        public string DetailText =>
            string.IsNullOrWhiteSpace(Name)
                ? "（未选中分区）"
                : $"分区名: {Name}\n" +
                  $"目标路径: {ByNamePath ?? "-"}\n" +
                  $"块设备: {BlockDevicePath ?? "-"}\n" +
                  $"底层设备: {BlockDeviceName ?? "-"}\n" +
                  $"大小: {SizeDisplay} ({SizeBytes} 字节)\n" +
                  $"挂载: {MountDisplay}{(IsMounted ? " — 挂载中刷写风险更高" : "")}\n" +
                  (IsMounted || string.IsNullOrWhiteSpace(SuggestedMountPoint)
                      ? string.Empty
                      : $"建议挂载点: {SuggestedMountPoint}\n") +
                  (!IsMounted && !string.IsNullOrWhiteSpace(DetectedFilesystem)
                      ? $"检测到文件系统: {DetectedFilesystem}\n"
                      : string.Empty) +
                  $"A/B 槽: {SlotDisplay}\n" +
                  $"风险: {(IsCritical ? "高 — 刷写可能导致无法开机" : "中 — 请确认镜像正确")}";

        public static bool IsCriticalPartitionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            var lower = name.ToLowerInvariant();
            return lower.Contains("uboot")
                || lower.Contains("boot")
                || lower.Contains("trust")
                || lower.Contains("system")
                || lower.Contains("recovery")
                || lower == "misc";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
            {
                return "未知";
            }

            if (bytes >= 1024L * 1024 * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
            }

            if (bytes >= 1024L * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }
    }
}
