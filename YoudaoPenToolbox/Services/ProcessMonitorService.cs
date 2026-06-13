using System;
using System.Threading;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class ProcessMonitorService
    {
        private readonly AdbService _adbService;
        private CancellationTokenSource _cts;

        public event EventHandler<ProcessSnapshot> ProcessesUpdated;

        public int PollIntervalMs { get; set; } = 5000;

        public ProcessMonitorService(AdbService adbService)
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

        public async Task<ProcessSnapshot> GetSnapshotAsync(string serial)
        {
            var output = await _adbService.ShellAsync(serial, "top -b -n 1").ConfigureAwait(false);
            var snapshot = TopOutputParser.Parse(output);

            if (snapshot.Processes.Count == 0)
            {
                return snapshot;
            }

            var enrichScript = BuildEnrichScript(snapshot.Processes);
            if (string.IsNullOrWhiteSpace(enrichScript))
            {
                return snapshot;
            }

            var enrichOutput = await _adbService.ShellAsync(serial, enrichScript).ConfigureAwait(false);
            ApplyExecutablePaths(snapshot.Processes, enrichOutput);
            return snapshot;
        }

        private async Task MonitorLoopAsync(string serial, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var snapshot = await GetSnapshotAsync(serial).ConfigureAwait(false);
                    ProcessesUpdated?.Invoke(this, snapshot);
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

        private static string BuildEnrichScript(System.Collections.Generic.IReadOnlyList<ProcessInfo> processes)
        {
            var lines = new System.Text.StringBuilder();
            var count = 0;
            foreach (var process in processes)
            {
                if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                {
                    continue;
                }

                lines.Append("readlink /proc/").Append(process.Pid).Append("/exe 2>/dev/null; echo");
                count++;
                if (count >= 40)
                {
                    break;
                }
            }

            return count == 0 ? null : lines.ToString();
        }

        private static void ApplyExecutablePaths(System.Collections.Generic.IReadOnlyList<ProcessInfo> processes, string enrichOutput)
        {
            if (string.IsNullOrWhiteSpace(enrichOutput))
            {
                return;
            }

            var paths = enrichOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var pathIndex = 0;
            foreach (var process in processes)
            {
                if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                {
                    continue;
                }

                if (pathIndex >= paths.Length)
                {
                    break;
                }

                var path = paths[pathIndex++].Trim();
                if (!string.IsNullOrWhiteSpace(path) && path.StartsWith("/", StringComparison.Ordinal))
                {
                    process.ExecutablePath = path;
                }
            }
        }
    }
}
