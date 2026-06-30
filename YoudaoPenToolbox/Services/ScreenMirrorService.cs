using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public sealed class ScreenMirrorFrameEventArgs : EventArgs
    {
        public ImageSource Frame { get; set; }
        public int FrameIndex { get; set; }
        public long FrameLatencyMs { get; set; }
        public double FramesPerSecond { get; set; }
    }

    public class ScreenMirrorService
    {
        public const string RemoteCapturePath = "/tmp/ypt_mirror.png";

        private readonly AdbService _adbService;
        private readonly MiniAppCliService _cliService;
        private readonly DeviceRemoteInputService _remoteInputService;
        private readonly SemaphoreSlim _captureLock = new SemaphoreSlim(1, 1);

        public ScreenMirrorService(AdbService adbService, MiniAppCliService cliService)
        {
            _adbService = adbService;
            _cliService = cliService;
            _remoteInputService = new DeviceRemoteInputService(adbService);
        }

        public DeviceRemoteInputService RemoteInput => _remoteInputService;

        public event EventHandler<ScreenMirrorFrameEventArgs> FrameReady;

        public async Task<bool> ProbeAsync(string serial, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await _cliService
                .ExecuteRawAsync(serial, $"capture {RemoteCapturePath}")
                .ConfigureAwait(false);

            if (!IsCaptureSuccessful(result))
            {
                await CleanupRemoteAsync(serial).ConfigureAwait(false);
                return false;
            }

            await CleanupRemoteAsync(serial).ConfigureAwait(false);
            return true;
        }

        public async Task RunMirrorLoopAsync(string serial, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                throw new ArgumentException("设备序列号不能为空。", nameof(serial));
            }

            var frameIndex = 0;
            var fpsStopwatch = Stopwatch.StartNew();
            var fpsFrameCount = 0;
            var currentFps = 0.0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _captureLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var capture = await _cliService
                            .ExecuteRawAsync(serial, $"capture {RemoteCapturePath}")
                            .ConfigureAwait(false);

                        if (!IsCaptureSuccessful(capture))
                        {
                            continue;
                        }

                        var frame = await PullFrameAsync(serial, cancellationToken).ConfigureAwait(false);
                        await CleanupRemoteAsync(serial).ConfigureAwait(false);

                        if (frame == null)
                        {
                            continue;
                        }

                        sw.Stop();
                        frameIndex++;
                        fpsFrameCount++;

                        if (fpsStopwatch.Elapsed.TotalSeconds >= 1.0)
                        {
                            currentFps = fpsFrameCount / fpsStopwatch.Elapsed.TotalSeconds;
                            fpsFrameCount = 0;
                            fpsStopwatch.Restart();
                        }

                        FrameReady?.Invoke(this, new ScreenMirrorFrameEventArgs
                        {
                            Frame = frame,
                            FrameIndex = frameIndex,
                            FrameLatencyMs = sw.ElapsedMilliseconds,
                            FramesPerSecond = currentFps
                        });
                    }
                    finally
                    {
                        _captureLock.Release();
                    }
                }
            }
            finally
            {
                await CleanupAsync(serial).ConfigureAwait(false);
            }
        }

        public async Task CleanupAsync(string serial)
        {
            await CleanupRemoteAsync(serial).ConfigureAwait(false);
            DeleteLocalArtifacts(serial);
        }

        public Task<bool> ProbeRemoteInputAsync(string serial)
        {
            return _remoteInputService.ProbeAsync(serial);
        }

        public Task<ScreenMirrorDisplayInfo> LoadDisplayInfoAsync(string serial)
        {
            return _remoteInputService.LoadDisplayInfoAsync(serial);
        }

        public Task PressHomeAsync(string serial)
        {
            return _remoteInputService.PressHomeAsync(serial);
        }

        public Task PressBackAsync(string serial)
        {
            return _remoteInputService.PressBackAsync(serial);
        }

        public Task TapAsync(string serial, int x, int y)
        {
            return _remoteInputService.TapAsync(serial, x, y);
        }

        public Task SwipeAsync(string serial, int startX, int startY, int endX, int endY)
        {
            return _remoteInputService.SwipeAsync(serial, startX, startY, endX, endY);
        }

        private async Task<ImageSource> PullFrameAsync(string serial, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localPath = GetLocalFramePath(serial);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? string.Empty);

            var pulled = await _adbService
                .PullFileAsync(serial, RemoteCapturePath, localPath)
                .ConfigureAwait(false);

            if (!pulled || !File.Exists(localPath))
            {
                return null;
            }

            return LoadBitmapFromFile(localPath);
        }

        private Task CleanupRemoteAsync(string serial)
        {
            return AppBackupService.TryDeleteRemoteFileAsync(_adbService, serial, RemoteCapturePath);
        }

        private static bool IsCaptureSuccessful(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return false;
            }

            if (result.IndexOf("capture failed", StringComparison.OrdinalIgnoreCase) >= 0
                || result.IndexOf("Error connect", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return result.IndexOf("\"ret\": 0", StringComparison.Ordinal) >= 0
                   || result.IndexOf("\"ret\":0", StringComparison.Ordinal) >= 0;
        }

        private static string GetLocalFramePath(string serial)
        {
            var safeSerial = AppBackupService.SanitizeFileName(serial);
            return Path.Combine(
                Path.GetTempPath(),
                "YoudaoPenToolbox",
                "ScreenMirror",
                safeSerial,
                "mirror.png");
        }

        private static void DeleteLocalArtifacts(string serial)
        {
            try
            {
                var directory = Path.GetDirectoryName(GetLocalFramePath(serial));
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }

        private static BitmapSource LoadBitmapFromFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();
                return frame;
            }
        }
    }
}
