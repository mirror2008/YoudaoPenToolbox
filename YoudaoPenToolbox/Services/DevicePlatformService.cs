using System;
using System.Linq;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class DevicePlatformService
    {
        private readonly AdbService _adbService;

        public DevicePlatformService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public async Task<LoliPlatformInfo> DetectLoliPlatformAsync(string serial)
        {
            var model = await _adbService.ShellAsync(serial, "cat /proc/device-tree/model 2>/dev/null").ConfigureAwait(false);
            var compatible = await _adbService.ShellAsync(serial, "cat /proc/device-tree/compatible 2>/dev/null | tr '\\0' ' '").ConfigureAwait(false);
            var osRelease = await _adbService.ShellAsync(serial, "cat /etc/os-release 2>/dev/null").ConfigureAwait(false);
            var hostname = await _adbService.ShellAsync(serial, "hostname 2>/dev/null").ConfigureAwait(false);

            var combined = string.Join(" ", new[] { model, compatible, osRelease, hostname }
                .Where(s => !string.IsNullOrWhiteSpace(s)))
                .ToLowerInvariant();

            var detail = $"model={model?.Trim()} | host={hostname?.Trim()}";

            if (IsRockchipPlatform(combined))
            {
                return new LoliPlatformInfo
                {
                    PlatformType = "rk",
                    PlatformLabel = "RK 芯片 (GStreamer)",
                    RepositorySubPath = "app/rk-gst",
                    DetectionDetail = detail
                };
            }

            if (IsCviPlatform(combined))
            {
                var subPath = DetectCviSubPath(combined);
                return new LoliPlatformInfo
                {
                    PlatformType = "cvi",
                    PlatformLabel = "CVI 芯片 (FFmpeg) · " + subPath.FolderLabel,
                    RepositorySubPath = subPath.Path,
                    DetectionDetail = detail
                };
            }

            if (ContainsSku(combined, "y02", "y05", "y07", "y08", "y09", "y11", "x7", "rk3562", "rk35"))
            {
                return new LoliPlatformInfo
                {
                    PlatformType = "rk",
                    PlatformLabel = "RK 芯片 (推测)",
                    RepositorySubPath = "app/rk-gst",
                    DetectionDetail = detail
                };
            }

            if (ContainsSku(combined, "a6", "a7"))
            {
                return new LoliPlatformInfo
                {
                    PlatformType = "cvi",
                    PlatformLabel = "CVI 芯片 A6/A7 (推测)",
                    RepositorySubPath = "app/cvi-ffmpeg/a6-a7",
                    DetectionDetail = detail
                };
            }

            if (ContainsSku(combined, "s7", "x7"))
            {
                return new LoliPlatformInfo
                {
                    PlatformType = "cvi",
                    PlatformLabel = "CVI 芯片 X5/S6/S7 (推测)",
                    RepositorySubPath = "app/cvi-ffmpeg/x5-s6-s7",
                    DetectionDetail = detail
                };
            }

            if (ContainsSku(combined, "x5", "s6"))
            {
                return new LoliPlatformInfo
                {
                    PlatformType = "cvi",
                    PlatformLabel = "CVI 芯片 X5/S6 (推测)",
                    RepositorySubPath = "app/cvi-ffmpeg/x5-s6",
                    DetectionDetail = detail
                };
            }

            return new LoliPlatformInfo
            {
                PlatformType = "unknown",
                PlatformLabel = "未能识别芯片平台",
                RepositorySubPath = null,
                DetectionDetail = detail
            };
        }

        private static bool IsRockchipPlatform(string text)
        {
            return text.IndexOf("rockchip", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk3562", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk3566", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk3568", StringComparison.Ordinal) >= 0
                   || text.IndexOf(" rk35", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk-gst", StringComparison.Ordinal) >= 0;
        }

        private static bool IsCviPlatform(string text)
        {
            return text.IndexOf("cvitek", StringComparison.Ordinal) >= 0
                   || text.IndexOf("cvi182", StringComparison.Ordinal) >= 0
                   || text.IndexOf("cv18", StringComparison.Ordinal) >= 0
                   || text.IndexOf(" cvi", StringComparison.Ordinal) >= 0;
        }

        private static (string Path, string FolderLabel) DetectCviSubPath(string text)
        {
            if (ContainsSku(text, "a6", "a7"))
            {
                return ("app/cvi-ffmpeg/a6-a7", "A6/A7");
            }

            if (ContainsSku(text, "s7"))
            {
                return ("app/cvi-ffmpeg/x5-s6-s7", "X5/S6/S7");
            }

            if (ContainsSku(text, "x5", "s6"))
            {
                return ("app/cvi-ffmpeg/x5-s6", "X5/S6");
            }

            return ("app/cvi-ffmpeg/x5-s6-s7", "X5/S6/S7 (默认)");
        }

        private static bool ContainsSku(string text, params string[] tokens)
        {
            return tokens.Any(token => text.IndexOf(token, StringComparison.Ordinal) >= 0);
        }
    }
}
