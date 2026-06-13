namespace YoudaoPenToolbox.Models
{
    public class DeviceStatus
    {
        public int BatteryLevel { get; set; } = -1;
        public string BatteryStatus { get; set; } = "未知";
        public bool IsCharging { get; set; }
        public double CpuUsagePercent { get; set; }
        public long TotalMemoryKb { get; set; }
        public long AvailableMemoryKb { get; set; }
        public long UsedMemoryKb => TotalMemoryKb > 0 ? TotalMemoryKb - AvailableMemoryKb : 0;
        public double MemoryUsagePercent => TotalMemoryKb > 0 ? UsedMemoryKb * 100.0 / TotalMemoryKb : 0;
        public double LoadAverage1 { get; set; }
        public double LoadAverage5 { get; set; }
        public double LoadAverage15 { get; set; }
        public string Uptime { get; set; }
        public string DiskDisplay { get; set; } = "未知";

        public string MemoryDisplay => TotalMemoryKb > 0
            ? $"{FormatMb(UsedMemoryKb)} / {FormatMb(TotalMemoryKb)} ({MemoryUsagePercent:F1}%)"
            : "未知";

        public string CpuDisplay => $"{CpuUsagePercent:F1}%";
        public string LoadDisplay => LoadAverage1 > 0 ? $"{LoadAverage1:F2} / {LoadAverage5:F2} / {LoadAverage15:F2}" : "未知";
        public string BatteryDisplay => BatteryLevel >= 0 ? $"{BatteryLevel}% {BatteryStatus}" : "未知";

        private static string FormatMb(long kb) => $"{kb / 1024.0:F1} MB";
    }
}
