using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class ProcessSnapshot
    {
        public string Summary { get; set; }
        public IReadOnlyList<ProcessInfo> Processes { get; set; } = Array.Empty<ProcessInfo>();
    }

    public static class TopOutputParser
    {
        private static readonly Regex CombinedVszRegex = new Regex(@"^(\d+m?)([\d.]+)$", RegexOptions.Compiled);

        public static ProcessSnapshot Parse(string output)
        {
            var snapshot = new ProcessSnapshot();
            if (string.IsNullOrWhiteSpace(output))
            {
                return snapshot;
            }

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var summaryLines = new List<string>();
            var processes = new List<ProcessInfo>();
            var inProcessTable = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var trimmedStart = line.TrimStart();
                if (IsProcessHeaderLine(trimmedStart))
                {
                    inProcessTable = true;
                    continue;
                }

                if (!inProcessTable)
                {
                    if (line.StartsWith("Mem:", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("CPU:", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("Load average:", StringComparison.OrdinalIgnoreCase))
                    {
                        summaryLines.Add(line.Trim());
                    }
                    else if (summaryLines.Count > 0 && LooksLikeProcessLine(trimmedStart))
                    {
                        inProcessTable = true;
                        var process = ParseProcessLine(trimmedStart);
                        if (process != null)
                        {
                            processes.Add(process);
                        }
                    }

                    continue;
                }

                var parsedProcess = ParseProcessLine(trimmedStart);
                if (parsedProcess != null)
                {
                    processes.Add(parsedProcess);
                }
            }

            snapshot.Summary = summaryLines.Count > 0
                ? string.Join("  |  ", summaryLines)
                : "暂无 top 摘要信息";
            snapshot.Processes = processes
                .OrderByDescending(p => p.MemoryPercent)
                .ThenByDescending(p => p.CpuPercent)
                .ToList();
            return snapshot;
        }

        private static bool IsProcessHeaderLine(string line)
        {
            return line.StartsWith("PID", StringComparison.OrdinalIgnoreCase)
                   && line.IndexOf("PPID", StringComparison.OrdinalIgnoreCase) >= 0
                   && line.IndexOf("COMMAND", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 8
                   && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                   && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private static ProcessInfo ParseProcessLine(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 8)
            {
                return null;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            {
                return null;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ppid))
            {
                return null;
            }

            var user = parts[2];
            var stat = parts[3];
            string virtualMemory;
            double memoryPercent;
            int cpuCore;
            double cpuPercent;
            int commandStartIndex;

            if (parts.Length >= 9 && double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                virtualMemory = parts[4];
                memoryPercent = ParseDouble(parts[5]);
                cpuCore = ParseInt(parts[6]);
                cpuPercent = ParseDouble(parts[7]);
                commandStartIndex = 8;
            }
            else
            {
                if (!TrySplitVsz(parts[4], out virtualMemory, out memoryPercent))
                {
                    return null;
                }

                cpuCore = ParseInt(parts[5]);
                cpuPercent = ParseDouble(parts[6]);
                commandStartIndex = 7;
            }

            var command = string.Join(" ", parts.Skip(commandStartIndex));
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            var executablePath = ExtractExecutablePath(command);
            return new ProcessInfo
            {
                Pid = pid,
                Ppid = ppid,
                User = user,
                Stat = stat,
                VirtualMemory = virtualMemory,
                MemoryPercent = memoryPercent,
                CpuCore = cpuCore,
                CpuPercent = cpuPercent,
                Command = command,
                ExecutablePath = executablePath
            };
        }

        private static bool TrySplitVsz(string token, out string virtualMemory, out double memoryPercent)
        {
            virtualMemory = token;
            memoryPercent = 0;

            var match = CombinedVszRegex.Match(token);
            if (!match.Success)
            {
                return double.TryParse(token.TrimEnd('m'), NumberStyles.Float, CultureInfo.InvariantCulture, out memoryPercent);
            }

            virtualMemory = match.Groups[1].Value;
            return double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out memoryPercent);
        }

        private static string ExtractExecutablePath(string command)
        {
            if (command.StartsWith("{", StringComparison.Ordinal))
            {
                var closeBrace = command.IndexOf('}');
                if (closeBrace >= 0 && closeBrace + 1 < command.Length)
                {
                    var pathPart = command.Substring(closeBrace + 1).Trim();
                    if (pathPart.StartsWith("/", StringComparison.Ordinal))
                    {
                        return pathPart.Split(' ')[0];
                    }
                }
            }

            if (command.StartsWith("/", StringComparison.Ordinal))
            {
                return command.Split(' ')[0];
            }

            return string.Empty;
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;
        }
    }
}
