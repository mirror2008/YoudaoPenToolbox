using System;

namespace YoudaoPenToolbox.Models
{
    public class PartitionIoProgress
    {
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public string Phase { get; set; }
        public double SpeedBytesPerSecond { get; set; }
        public TimeSpan? EstimatedRemaining { get; set; }

        public double Percent => TotalBytes > 0
            ? Math.Min(100.0, BytesTransferred * 100.0 / TotalBytes)
            : 0;

        public string Display
        {
            get
            {
                var sizePart = TotalBytes > 0
                    ? $"{Phase} {BytesTransferred * 100.0 / TotalBytes:F1}% ({FormatSize(BytesTransferred)} / {FormatSize(TotalBytes)})"
                    : $"{Phase} {FormatSize(BytesTransferred)}";

                if (SpeedBytesPerSecond > 0)
                {
                    sizePart += $" · {FormatSpeed(SpeedBytesPerSecond)}";
                }

                if (EstimatedRemaining.HasValue && EstimatedRemaining.Value.TotalSeconds >= 1)
                {
                    sizePart += $" · 剩余 {FormatDuration(EstimatedRemaining.Value)}";
                }

                return sizePart;
            }
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024L * 1024)
            {
                return $"{bytesPerSecond / 1024.0 / 1024.0:F2} MB/s";
            }

            if (bytesPerSecond >= 1024)
            {
                return $"{bytesPerSecond / 1024.0:F1} KB/s";
            }

            return $"{bytesPerSecond:F0} B/s";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }

            if (duration.TotalMinutes >= 1)
            {
                return $"{duration.Minutes}:{duration.Seconds:D2}";
            }

            return $"{Math.Max(1, (int)duration.TotalSeconds)} 秒";
        }

        private static string FormatSize(long bytes)
        {
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
