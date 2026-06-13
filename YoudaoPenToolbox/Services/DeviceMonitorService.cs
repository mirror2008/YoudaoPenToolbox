using System;
using System.Threading;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class DeviceMonitorService
    {
        private readonly AdbService _adbService;
        private CancellationTokenSource _cts;

        public event EventHandler<DeviceStatus> StatusUpdated;

        public int PollIntervalMs { get; set; } = 5000;

        public DeviceMonitorService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public void StartMonitoring(string serial)
        {
            StopMonitoring();
            _cts = new CancellationTokenSource();
            _ = MonitorLoopAsync(serial, _cts.Token);
        }

        public void StopMonitoring()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task MonitorLoopAsync(string serial, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var status = await _adbService.GetDeviceStatusAsync(serial).ConfigureAwait(false);
                    StatusUpdated?.Invoke(this, status);
                }
                catch
                {

                }

                try
                {
                    await Task.Delay(PollIntervalMs, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
