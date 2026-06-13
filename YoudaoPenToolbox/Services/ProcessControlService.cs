using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class ProcessControlResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class ProcessControlService
    {
        private readonly AdbService _adbService;

        public ProcessControlService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public bool IsProtectedProcess(ProcessInfo process, out string reason)
        {
            reason = null;
            if (process == null)
            {
                reason = "请先选择进程";
                return true;
            }

            if (process.Pid <= 1)
            {
                reason = "无法操作系统 init 进程";
                return true;
            }

            var identity = $"{process.Command} {process.ExecutablePath} {process.PathDisplay}";
            if (identity.IndexOf("adbd", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "adbd 受保护，终止会导致 ADB 断开";
                return true;
            }

            return false;
        }

        public async Task<IReadOnlyList<string>> GetProcessArgsAsync(string serial, ProcessInfo process)
        {
            if (process == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                var encoded = await _adbService.ShellAsync(serial,
                    $"base64 /proc/{process.Pid}/cmdline 2>/dev/null | tr -d '\\n\\r'").ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(encoded))
                {
                    var bytes = Convert.FromBase64String(encoded.Trim());
                    var args = SplitNullTerminated(bytes);
                    if (args.Count > 0)
                    {
                        return args;
                    }
                }
            }
            catch
            {

            }

            return BuildArgsFromProcess(process);
        }

        public async Task<string> KillProcessAsync(string serial, int pid)
        {
            var output = await _adbService.ShellAsync(serial, $"kill -15 {pid} 2>&1").ConfigureAwait(false);
            await Task.Delay(400).ConfigureAwait(false);

            var aliveCheck = await _adbService.ShellAsync(serial,
                $"kill -0 {pid} 2>/dev/null && echo ALIVE || echo DEAD").ConfigureAwait(false);
            if (aliveCheck != null && aliveCheck.IndexOf("ALIVE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var forceOutput = await _adbService.ShellAsync(serial, $"kill -9 {pid} 2>&1").ConfigureAwait(false);
                output = string.IsNullOrWhiteSpace(output)
                    ? forceOutput
                    : output + Environment.NewLine + forceOutput;
            }

            return output?.Trim();
        }

        public async Task<ProcessControlResult> RestartProcessAsync(string serial, ProcessInfo process)
        {
            var args = await GetProcessArgsAsync(serial, process).ConfigureAwait(false);
            if (args.Count == 0)
            {
                return new ProcessControlResult
                {
                    Success = false,
                    Message = "无法读取进程启动命令，无法重启"
                };
            }

            await KillProcessAsync(serial, process.Pid).ConfigureAwait(false);
            await Task.Delay(600).ConfigureAwait(false);

            var startCommand = BuildStartCommand(args);
            var output = await _adbService.ShellAsync(serial, startCommand).ConfigureAwait(false);
            return new ProcessControlResult
            {
                Success = true,
                Message = $"启动命令: {FormatArgs(args)}\r\n{output?.Trim()}"
            };
        }

        public static string FormatArgs(IReadOnlyList<string> args)
        {
            return args == null ? string.Empty : string.Join(" ", args);
        }

        private static IReadOnlyList<string> BuildArgsFromProcess(ProcessInfo process)
        {
            var command = process.Command?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(command))
            {
                if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                {
                    return new[] { process.ExecutablePath };
                }

                return Array.Empty<string>();
            }

            if (command.StartsWith("{", StringComparison.Ordinal))
            {
                var closeBrace = command.IndexOf('}');
                if (closeBrace >= 0 && closeBrace + 1 < command.Length)
                {
                    command = command.Substring(closeBrace + 1).Trim();
                }
            }

            return command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static List<string> SplitNullTerminated(byte[] bytes)
        {
            var args = new List<string>();
            if (bytes == null || bytes.Length == 0)
            {
                return args;
            }

            var start = 0;
            for (var i = 0; i <= bytes.Length; i++)
            {
                if (i == bytes.Length || bytes[i] == 0)
                {
                    if (i > start)
                    {
                        var value = Encoding.UTF8.GetString(bytes, start, i - start);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            args.Add(value);
                        }
                    }

                    start = i + 1;
                }
            }

            return args;
        }

        private static string BuildStartCommand(IReadOnlyList<string> args)
        {
            var quoted = string.Join(" ", args.Select(ShellQuote));
            return $"nohup {quoted} >/dev/null 2>&1 &";
        }

        private static string ShellQuote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "''";
            }

            return "'" + value.Replace("'", "'\\''") + "'";
        }
    }
}
