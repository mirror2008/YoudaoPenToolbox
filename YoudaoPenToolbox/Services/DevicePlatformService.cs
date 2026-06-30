using System;
using System.Linq;
using System.Text;
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

        public Task<LoliPlatformInfo> BuildFromUserChoiceAsync(string serial, bool useRockchip)
        {
            return BuildPlatformInfoAsync(serial, useRockchip, userSelected: true);
        }

        public async Task<LoliPlatformInfo> DetectLoliPlatformAsync(string serial)
        {
            var probe = await CollectCompatibilityProbeAsync(serial).ConfigureAwait(false);
            var model = await _adbService.ShellAsync(serial, "cat /proc/device-tree/model 2>/dev/null").ConfigureAwait(false);
            var hostname = await _adbService.ShellAsync(serial, "hostname 2>/dev/null").ConfigureAwait(false);

            var compatible = probe.DeviceTreeCompatible ?? string.Empty;
            var combined = BuildCombinedProbeText(probe, compatible, model, hostname);

            if (ShouldUseRockchipBranch(probe, combined))
            {
                return CreatePlatformInfo(probe, combined, model, hostname, useRockchip: true, userSelected: false);
            }

            if (ShouldUseCviBranch(probe, combined))
            {
                return CreatePlatformInfo(probe, combined, model, hostname, useRockchip: false, userSelected: false);
            }

            if (ContainsSku(combined, "x7", "x7pro", "y02", "y05", "y07", "y08", "y09", "y11", "rk3562", "rk3566", "rk3568", "rk35"))
            {
                return CreatePlatformInfo(probe, combined, model, hostname, useRockchip: true, userSelected: false, guessed: true);
            }

            if (ContainsSku(combined, "a6", "a7"))
            {
                return CreateCviPlatformInfo(probe, combined, model, hostname, "app/cvi-ffmpeg/a6-a7", "A6/A7", guessed: true);
            }

            if (ContainsSku(combined, "s7"))
            {
                return CreateCviPlatformInfo(probe, combined, model, hostname, "app/cvi-ffmpeg/x5-s6-s7", "X5/S6/S7", guessed: true);
            }

            if (ContainsSku(combined, "x5", "s6"))
            {
                return CreateCviPlatformInfo(probe, combined, model, hostname, "app/cvi-ffmpeg/x5-s6", "X5/S6", guessed: true);
            }

            return CreateUnsupportedPlatformInfo(probe, combined, model, hostname);
        }

        private async Task<LoliPlatformInfo> BuildPlatformInfoAsync(string serial, bool useRockchip, bool userSelected)
        {
            var probe = await CollectCompatibilityProbeAsync(serial).ConfigureAwait(false);
            var model = await _adbService.ShellAsync(serial, "cat /proc/device-tree/model 2>/dev/null").ConfigureAwait(false);
            var hostname = await _adbService.ShellAsync(serial, "hostname 2>/dev/null").ConfigureAwait(false);
            var compatible = probe.DeviceTreeCompatible ?? string.Empty;
            var combined = BuildCombinedProbeText(probe, compatible, model, hostname);
            return CreatePlatformInfo(probe, combined, model, hostname, useRockchip, userSelected);
        }

        private static string BuildCombinedProbeText(
            DeviceCompatibilityReport probe,
            string compatible,
            string model,
            string hostname)
        {
            return string.Join(" ", new[]
                {
                    probe.UnameMachine,
                    compatible,
                    model,
                    hostname,
                    probe.UsrBinListing,
                    probe.DevListing
                })
                .ToLowerInvariant();
        }

        private static LoliPlatformInfo CreatePlatformInfo(
            DeviceCompatibilityReport probe,
            string combined,
            string model,
            string hostname,
            bool useRockchip,
            bool userSelected,
            bool guessed = false)
        {
            if (useRockchip)
            {
                var suffix = userSelected ? "用户选择" : guessed ? "型号推测" : string.Empty;
                var label = string.IsNullOrEmpty(suffix)
                    ? "RK 芯片 · GStreamer (rk-gst)"
                    : $"RK 芯片 · GStreamer (rk-gst，{suffix})";
                return CreateRockchipPlatformInfo(probe, combined, model, hostname, label);
            }

            var subPath = DetectCviSubPath(combined, model, hostname);
            var cviSuffix = userSelected ? "用户选择" : guessed ? "型号推测" : string.Empty;
            var cviLabel = string.IsNullOrEmpty(cviSuffix)
                ? "CVI 芯片 · FFmpeg (cvi-ffmpeg) · " + subPath.FolderLabel
                : $"CVI 芯片 · FFmpeg (cvi-ffmpeg/{subPath.FolderLabel}，{cviSuffix})";
            return CreateCviPlatformInfo(probe, combined, model, hostname, subPath.Path, subPath.FolderLabel, cviLabel);
        }

        private static LoliPlatformInfo CreateRockchipPlatformInfo(
            DeviceCompatibilityReport probe,
            string combined,
            string model,
            string hostname,
            string platformLabel)
        {
            return new LoliPlatformInfo
            {
                PlatformType = "rk",
                PlatformLabel = platformLabel,
                RepositorySubPath = "app/rk-gst",
                DetectionDetail = BuildDetectionDetail(probe, model, hostname),
                CompatibilityReport = probe.FormatForDisplay(),
                Probe = probe
            };
        }

        private static LoliPlatformInfo CreateCviPlatformInfo(
            DeviceCompatibilityReport probe,
            string combined,
            string model,
            string hostname,
            string repositorySubPath,
            string folderLabel,
            bool guessed = false)
        {
            var label = guessed
                ? $"CVI 芯片 · FFmpeg (cvi-ffmpeg/{folderLabel}，型号推测)"
                : "CVI 芯片 · FFmpeg (cvi-ffmpeg) · " + folderLabel;
            return CreateCviPlatformInfo(probe, combined, model, hostname, repositorySubPath, folderLabel, label);
        }

        private static LoliPlatformInfo CreateCviPlatformInfo(
            DeviceCompatibilityReport probe,
            string combined,
            string model,
            string hostname,
            string repositorySubPath,
            string folderLabel,
            string platformLabel)
        {
            return new LoliPlatformInfo
            {
                PlatformType = "cvi",
                PlatformLabel = platformLabel,
                RepositorySubPath = repositorySubPath,
                DetectionDetail = BuildDetectionDetail(probe, model, hostname),
                CompatibilityReport = probe.FormatForDisplay(),
                Probe = probe
            };
        }

        private static LoliPlatformInfo CreateUnsupportedPlatformInfo(
            DeviceCompatibilityReport probe,
            string combined,
            string model,
            string hostname)
        {
            return new LoliPlatformInfo
            {
                PlatformType = "unknown",
                PlatformLabel = "未能识别芯片平台",
                RepositorySubPath = null,
                DetectionDetail = BuildDetectionDetail(probe, model, hostname),
                CompatibilityReport = probe.FormatForDisplay(),
                Probe = probe
            };
        }

        private static string BuildDetectionDetail(DeviceCompatibilityReport probe, string model, string hostname)
        {
            var detailBuilder = new StringBuilder();
            detailBuilder.Append("多媒体框架: ");
            if (probe.GstLaunchAvailable)
            {
                detailBuilder.Append("GStreamer (gst-launch-1.0 可用)");
            }
            else if (probe.HasFfmpegInUsrBin)
            {
                detailBuilder.Append("FFmpeg (/usr/bin/ffmpeg 存在)");
            }
            else
            {
                detailBuilder.Append("未检测到 GStreamer / FFmpeg");
            }

            detailBuilder.Append(" | miniapp_cli: ");
            detailBuilder.Append(probe.MiniappCliAvailable ? "可用" : "不可用或未找到");
            detailBuilder.Append(" | model=").Append(model?.Trim());
            detailBuilder.Append(" | host=").Append(hostname?.Trim());
            return detailBuilder.ToString();
        }

        public async Task<DeviceCompatibilityReport> CollectCompatibilityProbeAsync(string serial)
        {
            var unameTask = _adbService.ShellAsync(serial, "uname -m");
            var compatibleTask = _adbService.ShellAsync(serial, "cat /proc/device-tree/compatible 2>/dev/null | tr '\\0' ' '");
            var usrBinTask = _adbService.ShellAsync(serial, "ls -la /usr/bin/ 2>/dev/null");
            var devTask = _adbService.ShellAsync(serial, "ls /dev 2>/dev/null");
            var gstTask = _adbService.ShellAsync(serial, "gst-launch-1.0 2>&1 | head -n 10");
            var cliTask = _adbService.ShellAsync(serial, "miniapp_cli 2>&1 | head -n 12");

            await Task.WhenAll(unameTask, compatibleTask, usrBinTask, devTask, gstTask, cliTask).ConfigureAwait(false);

            return new DeviceCompatibilityReport
            {
                UnameMachine = unameTask.Result,
                DeviceTreeCompatible = compatibleTask.Result,
                UsrBinListing = usrBinTask.Result,
                DevListing = devTask.Result,
                GstLaunchOutput = gstTask.Result,
                MiniappCliOutput = cliTask.Result
            };
        }

        private static bool ShouldUseRockchipBranch(DeviceCompatibilityReport probe, string combined)
        {
            if (probe.GstLaunchAvailable)
            {
                return true;
            }

            return IsRockchipPlatform(combined);
        }

        private static bool ShouldUseCviBranch(DeviceCompatibilityReport probe, string combined)
        {
            if (probe.HasFfmpegInUsrBin && !probe.GstLaunchAvailable)
            {
                return true;
            }

            return IsCviPlatform(combined);
        }

        private static bool IsRockchipPlatform(string text)
        {
            return text.IndexOf("rockchip", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk3562", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk3566", StringComparison.Ordinal) >= 0
                   || text.IndexOf("rk3568", StringComparison.Ordinal) >= 0
                   || text.IndexOf(" rk35", StringComparison.Ordinal) >= 0;
        }

        private static bool IsCviPlatform(string text)
        {
            return text.IndexOf("cvitek", StringComparison.Ordinal) >= 0
                   || text.IndexOf("cvi182", StringComparison.Ordinal) >= 0
                   || text.IndexOf("cv18", StringComparison.Ordinal) >= 0
                   || text.IndexOf(" cvi", StringComparison.Ordinal) >= 0;
        }

        private static (string Path, string FolderLabel) DetectCviSubPath(string text, string model, string hostname)
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
