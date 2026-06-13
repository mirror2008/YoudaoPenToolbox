using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class DeviceWatchService
    {
        private readonly AdbService _adbService;
        private CancellationTokenSource _cts;

        public event EventHandler<IReadOnlyList<DeviceInfo>> DevicesUpdated;

        public int PollIntervalMs { get; set; } = 5000;

        public DeviceWatchService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            _ = WatchLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task WatchLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var devices = await _adbService.GetDevicesQuickAsync().ConfigureAwait(false);
                    DevicesUpdated?.Invoke(this, devices);
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
