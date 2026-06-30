using System;
using System.Threading.Tasks;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class DeviceRemoteInputService
    {
        private const int TapDelayMs = 35;
        private const int SwipeStepDelayMs = 25;

        private readonly AdbService _adbService;

        public DeviceRemoteInputService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public async Task<bool> ProbeAsync(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
            {
                return false;
            }

            var probe = await _adbService
                .ShellAsync(serial, "which send_event hal-key 2>/dev/null")
                .ConfigureAwait(false);

            return !string.IsNullOrWhiteSpace(probe)
                   && probe.IndexOf("send_event", StringComparison.Ordinal) >= 0
                   && probe.IndexOf("hal-key", StringComparison.Ordinal) >= 0;
        }

        public async Task<ScreenMirrorDisplayInfo> LoadDisplayInfoAsync(string serial)
        {
            var json = await _adbService
                .ShellAsync(serial, "cat /etc/miniapp/resources/cfg.json 2>/dev/null")
                .ConfigureAwait(false);
            return ScreenMirrorCoordinateHelper.ParseMiniAppConfig(json);
        }

        public async Task PressHomeAsync(string serial)
        {
            await _adbService.ShellAsync(serial, "hal-key 1 2>/dev/null").ConfigureAwait(false);
        }

        public async Task PressBackAsync(string serial)
        {
            await _adbService.ShellAsync(serial, "send_event menu press 2>/dev/null").ConfigureAwait(false);
            await Task.Delay(TapDelayMs).ConfigureAwait(false);
            await _adbService.ShellAsync(serial, "send_event menu release 2>/dev/null").ConfigureAwait(false);
        }

        public async Task SwipeBackAsync(string serial, ScreenMirrorDisplayInfo displayInfo)
        {
            if (displayInfo == null || !displayInfo.IsValid)
            {
                await PressBackAsync(serial).ConfigureAwait(false);
                return;
            }

            var startX = Math.Max(8, displayInfo.Width / 40);
            var endX = Math.Max(startX + 40, displayInfo.Width / 3);
            var y = displayInfo.Height / 2;
            ScreenMirrorCoordinateHelper.MapDisplayToTouch(displayInfo, startX, y, out var touchStartX, out var touchStartY);
            ScreenMirrorCoordinateHelper.MapDisplayToTouch(displayInfo, endX, y, out var touchEndX, out var touchEndY);
            await SwipeAsync(serial, touchStartX, touchStartY, touchEndX, touchEndY).ConfigureAwait(false);
        }

        public async Task TapAsync(string serial, int x, int y)
        {
            await _adbService.ShellAsync(serial, $"send_event touch press {x} {y} 2>/dev/null").ConfigureAwait(false);
            await Task.Delay(TapDelayMs).ConfigureAwait(false);
            await _adbService.ShellAsync(serial, $"send_event touch release {x} {y} 2>/dev/null").ConfigureAwait(false);
        }

        public async Task SwipeAsync(string serial, int startX, int startY, int endX, int endY)
        {
            await _adbService.ShellAsync(serial, $"send_event touch press {startX} {startY} 2>/dev/null").ConfigureAwait(false);
            await Task.Delay(SwipeStepDelayMs).ConfigureAwait(false);
            await _adbService.ShellAsync(serial, $"send_event touch slip {endX} {endY} 2>/dev/null").ConfigureAwait(false);
            await Task.Delay(SwipeStepDelayMs).ConfigureAwait(false);
            await _adbService.ShellAsync(serial, $"send_event touch release {endX} {endY} 2>/dev/null").ConfigureAwait(false);
        }
    }
}
