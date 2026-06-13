using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class AdbService
    {
        private string _adbPath;

        public string AdbPath
        {
            get => _adbPath;
            set => _adbPath = string.IsNullOrWhiteSpace(value) ? FindAdbPath() : value;
        }

        public AdbService()
        {
            AdbPath = FindAdbPath();
        }

        public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(bool fillProperties = true)
        {
            var output = await RunAdbAsync("devices -l").ConfigureAwait(false);
            var devices = new List<DeviceInfo>();

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var serial = parts[0];
                var state = parts[1];
                var device = new DeviceInfo
                {
                    Serial = serial,
                    State = state
                };

                var modelMatch = Regex.Match(line, @"model:(\S+)");
                if (modelMatch.Success)
                {
                    device.Model = modelMatch.Groups[1].Value.Replace('_', ' ');
                }

                var productMatch = Regex.Match(line, @"product:(\S+)");
                if (productMatch.Success)
                {
                    device.ProductName = productMatch.Groups[1].Value;
                }

                if (fillProperties && state == "device")
                {
                    await FillDevicePropertiesAsync(device).ConfigureAwait(false);
                }

                devices.Add(device);
            }

            return devices;
        }

        public Task<IReadOnlyList<DeviceInfo>> GetDevicesQuickAsync()
        {
            return GetDevicesAsync(false);
        }

        public async Task<DeviceStatus> GetDeviceStatusAsync(string serial)
        {
            var status = new DeviceStatus();

            await ParseBatteryAsync(serial, status).ConfigureAwait(false);

            var memOutput = await ShellAsync(serial, "cat /proc/meminfo").ConfigureAwait(false);
            ParseMemory(memOutput, status);

            var cpuOutput = await ShellAsync(serial, "cat /proc/stat").ConfigureAwait(false);
            status.CpuUsagePercent = await GetCpuUsageAsync(serial, cpuOutput).ConfigureAwait(false);

            var loadAvg = await ShellAsync(serial, "cat /proc/loadavg").ConfigureAwait(false);
            ParseLoadAverage(loadAvg, status);

            var uptime = await ShellAsync(serial, "uptime").ConfigureAwait(false);
            status.Uptime = uptime?.Trim();

            var diskOutput = await ShellAsync(serial, "df -h /userdisk 2>/dev/null | tail -1").ConfigureAwait(false);
            status.DiskDisplay = ParseDiskUsage(diskOutput);

            return status;
        }

        public Task<string> ShellAsync(string serial, string command)
        {
            var args = string.IsNullOrEmpty(serial)
                ? $"shell {command}"
                : $"-s {serial} shell {command}";
            return RunAdbAsync(args);
        }

        public static bool IsShellAuthRequired(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return true;
            }

            if (output.IndexOf("toolbox_probe_ok", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return output.IndexOf("adb shell auth", StringComparison.OrdinalIgnoreCase) >= 0
                || output.IndexOf("login with", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task<bool> IsShellAccessibleAsync(string serial)
        {
            var probe = await ShellAsync(serial, "echo toolbox_probe_ok").ConfigureAwait(false);
            return probe != null
                && probe.IndexOf("toolbox_probe_ok", StringComparison.Ordinal) >= 0;
        }

        public Task<string> RunDeviceCommandAsync(string serial, string adbArguments)
        {
            if (string.IsNullOrWhiteSpace(adbArguments))
            {
                return Task.FromResult(string.Empty);
            }

            var args = adbArguments.Trim();
            if (args.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Substring(4).Trim();
            }

            if (!string.IsNullOrEmpty(serial))
            {
                if (args.StartsWith("-s ", StringComparison.OrdinalIgnoreCase))
                {
                    args = Regex.Replace(args, @"^-s\s+\S+\s+", string.Empty);
                }

                args = $"-s {serial} {args}";
            }

            return RunAdbAsync(args);
        }

        public Task<string> RebootAsync(string serial)
        {
            return ShellAsync(serial, "sync; reboot");
        }

        public Task<string> ShutdownAsync(string serial)
        {
            return ShellAsync(serial, "sync; poweroff");
        }

        public Task<string> OpenInteractiveShellAsync(string serial)
        {
            var args = string.IsNullOrEmpty(serial) ? "shell" : $"-s {serial} shell";
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = args,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                Process.Start(psi);
                return "已打开 ADB 交互式 Shell 窗口";
            });
        }

        public async Task<bool> PushFileAsync(string serial, string localPath, string remotePath)
        {
            var args = string.IsNullOrEmpty(serial)
                ? $"push \"{localPath}\" \"{remotePath}\""
                : $"-s {serial} push \"{localPath}\" \"{remotePath}\"";

            var output = await RunAdbAsync(args).ConfigureAwait(false);
            return output.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0
                   && output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public async Task<bool> PullFileAsync(string serial, string remotePath, string localPath)
        {
            var args = string.IsNullOrEmpty(serial)
                ? $"pull \"{remotePath}\" \"{localPath}\""
                : $"-s {serial} pull \"{remotePath}\" \"{localPath}\"";

            var output = await RunAdbAsync(args).ConfigureAwait(false);
            return output.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public async Task<string> ReadRemoteTextFileAsync(string serial, string remotePath)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"youdao_pen_{Guid.NewGuid():N}.tmp");
            try
            {
                var pulled = await PullFileAsync(serial, remotePath, tempFile).ConfigureAwait(false);
                if (!File.Exists(tempFile))
                {
                    return null;
                }

                var info = new FileInfo(tempFile);
                if (!pulled && info.Length == 0)
                {
                    return null;
                }

                return File.ReadAllText(tempFile, Encoding.UTF8);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {

                }
            }
        }

        public Task<string> RunAdbAsync(string arguments)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(psi))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(120000);

                    var combined = string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
                    if (!string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(output))
                    {
                        combined = output.Trim() + Environment.NewLine + error.Trim();
                    }

                    return combined;
                }
            });
        }

        private async Task FillDevicePropertiesAsync(DeviceInfo device)
        {
            var probe = await ShellAsync(device.Serial, "echo toolbox_probe_ok").ConfigureAwait(false);
            if (IsShellAuthRequired(probe))
            {
                device.Model = null;
                device.Hostname = null;
                device.Brand = "Youdao";
                device.Manufacturer = "NetEase Youdao";
                device.AndroidVersion = "需解锁 ADB";
                device.Platform = null;
                return;
            }

            var model = await GetPropAsync(device.Serial, "ro.product.model").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(model) && !model.Contains("not found"))
            {
                device.Model = model;
            }
            else
            {
                var hostname = await ShellAsync(device.Serial, "hostname").ConfigureAwait(false);
                var dtModel = await ShellAsync(device.Serial, "cat /proc/device-tree/model 2>/dev/null").ConfigureAwait(false);
                device.Model = !string.IsNullOrWhiteSpace(dtModel)
                    ? dtModel.Trim().TrimEnd('\0')
                    : hostname?.Trim();
            }

            device.Brand = await GetPropAsync(device.Serial, "ro.product.brand").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(device.Brand) || device.Brand.Contains("not found"))
            {
                device.Brand = "Youdao";
            }

            device.Manufacturer = await GetPropAsync(device.Serial, "ro.product.manufacturer").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(device.Manufacturer) || device.Manufacturer.Contains("not found"))
            {
                device.Manufacturer = "NetEase Youdao";
            }

            device.AndroidVersion = await GetPropAsync(device.Serial, "ro.build.version.release").ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(device.AndroidVersion) || device.AndroidVersion.Contains("not found"))
            {
                var osRelease = await ShellAsync(device.Serial, "grep PRETTY_NAME /etc/os-release 2>/dev/null | cut -d= -f2 | tr -d '\"'").ConfigureAwait(false);
                var kernel = await ShellAsync(device.Serial, "uname -r").ConfigureAwait(false);
                device.AndroidVersion = !string.IsNullOrWhiteSpace(osRelease)
                    ? osRelease.Trim()
                    : $"Linux {kernel?.Trim()}";
            }

            device.ProductName = await GetPropAsync(device.Serial, "ro.product.name").ConfigureAwait(false) ?? device.ProductName;
            device.Hostname = (await ShellAsync(device.Serial, "hostname").ConfigureAwait(false))?.Trim();
            device.Platform = (await ShellAsync(device.Serial, "uname -m").ConfigureAwait(false))?.Trim();
        }

        private async Task<string> GetPropAsync(string serial, string prop)
        {
            var value = await ShellAsync(serial, $"getprop {prop}").ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private async Task ParseBatteryAsync(string serial, DeviceStatus status)
        {
            var capacity = await ShellAsync(serial, "cat /sys/class/power_supply/battery/capacity 2>/dev/null").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(capacity) && int.TryParse(capacity.Trim(), out var level))
            {
                status.BatteryLevel = level;
            }
            else
            {
                var dumpsys = await ShellAsync(serial, "dumpsys battery 2>/dev/null").ConfigureAwait(false);
                ParseAndroidBattery(dumpsys, status);
            }

            var batteryStatus = await ShellAsync(serial, "cat /sys/class/power_supply/battery/status 2>/dev/null").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(batteryStatus))
            {
                status.BatteryStatus = TranslateBatteryStatus(batteryStatus.Trim());
                status.IsCharging = batteryStatus.IndexOf("Charging", StringComparison.OrdinalIgnoreCase) >= 0
                                    || batteryStatus.IndexOf("Full", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            var acOnline = await ShellAsync(serial, "cat /sys/class/power_supply/ac/online 2>/dev/null").ConfigureAwait(false);
            if (acOnline?.Trim() == "1")
            {
                status.IsCharging = true;
                if (status.BatteryStatus == "未知" || status.BatteryStatus == "放电中")
                {
                    status.BatteryStatus = "充电中";
                }
            }
        }

        private static string TranslateBatteryStatus(string status)
        {
            switch (status.ToLowerInvariant())
            {
                case "charging": return "充电中";
                case "discharging": return "放电中";
                case "full": return "已充满";
                case "not charging": return "未充电";
                default: return status;
            }
        }

        private static void ParseAndroidBattery(string output, DeviceStatus status)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("level:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(trimmed.Split(':')[1].Trim(), out var level);
                    status.BatteryLevel = level;
                }
                else if (trimmed.StartsWith("status:", StringComparison.OrdinalIgnoreCase))
                {
                    var code = trimmed.Split(':')[1].Trim();
                    status.BatteryStatus = code switch
                    {
                        "2" => "充电中",
                        "3" => "放电中",
                        "4" => "未充电",
                        "5" => "已充满",
                        _ => "未知"
                    };
                    status.IsCharging = code == "2" || code == "5";
                }
            }
        }

        private static void ParseMemory(string output, DeviceStatus status)
        {
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    status.TotalMemoryKb = ParseKbValue(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                {
                    status.AvailableMemoryKb = ParseKbValue(line);
                }
            }

            if (status.AvailableMemoryKb == 0)
            {
                long free = 0, buffers = 0, cached = 0;
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("MemFree:", StringComparison.OrdinalIgnoreCase))
                        free = ParseKbValue(line);
                    else if (line.StartsWith("Buffers:", StringComparison.OrdinalIgnoreCase))
                        buffers = ParseKbValue(line);
                    else if (line.StartsWith("Cached:", StringComparison.OrdinalIgnoreCase))
                        cached = ParseKbValue(line);
                }
                status.AvailableMemoryKb = free + buffers + cached;
            }
        }

        private static void ParseLoadAverage(string output, DeviceStatus status)
        {
            var parts = output?.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts != null && parts.Length >= 3
                && double.TryParse(parts[0], out var load1)
                && double.TryParse(parts[1], out var load5)
                && double.TryParse(parts[2], out var load15))
            {
                status.LoadAverage1 = load1;
                status.LoadAverage5 = load5;
                status.LoadAverage15 = load15;
            }
        }

        private static string ParseDiskUsage(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return "未知";
            }

            var parts = output.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 5)
            {
                return $"{parts[2]} / {parts[1]} ({parts[4]})";
            }

            return output.Trim();
        }

        private static long ParseKbValue(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && long.TryParse(parts[1], out var val) ? val : 0;
        }

        private async Task<double> GetCpuUsageAsync(string serial, string firstSample)
        {
            var first = ParseCpuStat(firstSample);
            if (first == null)
            {
                return 0;
            }

            await Task.Delay(500).ConfigureAwait(false);

            var secondOutput = await ShellAsync(serial, "cat /proc/stat").ConfigureAwait(false);
            var second = ParseCpuStat(secondOutput);
            if (second == null)
            {
                return 0;
            }

            var totalDelta = second.Total - first.Total;
            var idleDelta = second.Idle - first.Idle;

            if (totalDelta <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, (totalDelta - idleDelta) * 100.0 / totalDelta));
        }

        private static CpuStat ParseCpuStat(string output)
        {
            var cpuLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.OrdinalIgnoreCase));

            if (cpuLine == null)
            {
                return null;
            }

            var values = cpuLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(v => long.TryParse(v, out var n) ? n : 0L)
                .ToArray();

            if (values.Length < 4)
            {
                return null;
            }

            var idle = values[3] + (values.Length > 4 ? values[4] : 0);
            var total = values.Sum();
            return new CpuStat { Idle = idle, Total = total };
        }

        private class CpuStat
        {
            public long Idle { get; set; }
            public long Total { get; set; }
        }

        public Task ExtractBlockDeviceToFileAsync(
            string serial,
            string blockDevicePath,
            string localFilePath,
            long expectedSizeBytes,
            IProgress<PartitionIoProgress> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(blockDevicePath))
            {
                throw new ArgumentException("块设备路径不能为空", nameof(blockDevicePath));
            }

            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                throw new ArgumentException("本地保存路径不能为空", nameof(localFilePath));
            }

            const int blockSize = 1024 * 1024;
            var quoted = QuoteShellSingle(blockDevicePath);
            var shellCommand = $"dd if={quoted} bs={blockSize} 2>/dev/null";

            return Task.Run(() =>
            {
                RunShellBinaryTransfer(
                    serial,
                    shellCommand,
                    localFilePath,
                    expectedSizeBytes,
                    readFromDevice: true,
                    progress,
                    cancellationToken);
            }, cancellationToken);
        }

        public Task WriteBlockDeviceFromFileAsync(
            string serial,
            string blockDevicePath,
            string localFilePath,
            IProgress<PartitionIoProgress> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(blockDevicePath))
            {
                throw new ArgumentException("块设备路径不能为空", nameof(blockDevicePath));
            }

            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            {
                throw new FileNotFoundException("找不到要刷入的镜像文件", localFilePath ?? string.Empty);
            }

            const int blockSize = 1024 * 1024;
            var quoted = QuoteShellSingle(blockDevicePath);
            var shellCommand = $"dd of={quoted} bs={blockSize} conv=fsync 2>/dev/null";
            var totalBytes = new FileInfo(localFilePath).Length;

            return Task.Run(() =>
            {
                RunShellBinaryTransfer(
                    serial,
                    shellCommand,
                    localFilePath,
                    totalBytes,
                    readFromDevice: false,
                    progress,
                    cancellationToken);
            }, cancellationToken);
        }

        private void RunShellBinaryTransfer(
            string serial,
            string shellCommand,
            string localFilePath,
            long totalBytes,
            bool readFromDevice,
            IProgress<PartitionIoProgress> progress,
            CancellationToken cancellationToken)
        {
            var args = string.IsNullOrEmpty(serial)
                ? $"shell {shellCommand}"
                : $"-s {serial} shell {shellCommand}";

            var psi = new ProcessStartInfo
            {
                FileName = AdbPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = !readFromDevice,
                RedirectStandardOutput = readFromDevice,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var progressState = new PartitionProgressState();

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("无法启动 ADB 进程");
                }

                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch
                    {

                    }
                }))
                {
                    var stderr = process.StandardError.ReadToEndAsync();

                    if (readFromDevice)
                    {
                        var directory = Path.GetDirectoryName(localFilePath);
                        if (!string.IsNullOrWhiteSpace(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        using (var output = File.Create(localFilePath))
                        {
                            var buffer = new byte[1024 * 1024];
                            var transferred = 0L;
                            Stream input = process.StandardOutput.BaseStream;

                            while (true)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var read = input.Read(buffer, 0, buffer.Length);
                                if (read <= 0)
                                {
                                    break;
                                }

                                output.Write(buffer, 0, read);
                                transferred += read;
                                ReportProgress(progress, progressState, "提取中", transferred, totalBytes);
                            }
                        }
                    }
                    else
                    {
                        using (var input = File.OpenRead(localFilePath))
                        {
                            var buffer = new byte[1024 * 1024];
                            var transferred = 0L;
                            Stream output = process.StandardInput.BaseStream;

                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                output.Write(buffer, 0, read);
                                transferred += read;
                                ReportProgress(progress, progressState, "刷入中", transferred, totalBytes);
                            }

                            process.StandardInput.Close();
                        }
                    }

                    if (!process.WaitForExit(Timeout.Infinite))
                    {
                        throw new TimeoutException("ADB 分区传输超时");
                    }

                    stderr.Wait();
                    if (process.ExitCode != 0)
                    {
                        var err = stderr.Result?.Trim();
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(err)
                            ? $"ADB 分区传输失败 (exit {process.ExitCode})"
                            : err);
                    }
                }
            }
        }

        private sealed class PartitionProgressState
        {
            public long LastBytes { get; set; }
            public DateTime LastReportUtc { get; set; } = DateTime.UtcNow;
            public double SmoothedSpeed { get; set; }
        }

        private static void ReportProgress(
            IProgress<PartitionIoProgress> progress,
            PartitionProgressState state,
            string phase,
            long transferred,
            long totalBytes)
        {
            var now = DateTime.UtcNow;
            var elapsedSeconds = (now - state.LastReportUtc).TotalSeconds;
            double speed = state.SmoothedSpeed;
            TimeSpan? eta = null;

            if (elapsedSeconds >= 0.25 && transferred > state.LastBytes)
            {
                var instantSpeed = (transferred - state.LastBytes) / elapsedSeconds;
                speed = state.SmoothedSpeed <= 0
                    ? instantSpeed
                    : state.SmoothedSpeed * 0.7 + instantSpeed * 0.3;
                state.SmoothedSpeed = speed;
                state.LastBytes = transferred;
                state.LastReportUtc = now;

                if (totalBytes > transferred && speed > 0)
                {
                    eta = TimeSpan.FromSeconds((totalBytes - transferred) / speed);
                }
            }

            progress?.Report(new PartitionIoProgress
            {
                Phase = phase,
                BytesTransferred = transferred,
                TotalBytes = totalBytes,
                SpeedBytesPerSecond = speed,
                EstimatedRemaining = eta
            });
        }

        private static string QuoteShellSingle(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\\''") + "'";
        }

        public static string FindAdbPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "adb.exe"),
                Path.Combine(baseDir, "tools", "adb.exe"),
                Path.Combine(baseDir, "platform-tools", "adb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Android", "android-sdk", "platform-tools", "adb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return Path.Combine(baseDir, "adb.exe");
        }
    }
}
