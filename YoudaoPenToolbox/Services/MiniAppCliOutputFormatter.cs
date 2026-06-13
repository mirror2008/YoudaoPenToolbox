using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace YoudaoPenToolbox.Services
{
    public static class MiniAppCliOutputFormatter
    {
        public static string Format(string commandName, string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                return "（无输出）";
            }

            var text = rawOutput.Trim();
            var lowerCmd = commandName?.ToLowerInvariant() ?? string.Empty;

            if (text.IndexOf("Usage:", StringComparison.OrdinalIgnoreCase) >= 0
                && text.IndexOf("miniapp_cli", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FormatUsageHelp(text);
            }

            switch (lowerCmd)
            {
                case "memoryapp":
                    return FormatMemoryApp(text);
                case "memoryusage":
                case "memoryusagegc":
                    return FormatQuickJsMemory(text, lowerCmd == "memoryusagegc");
                case "install":
                case "uninstall":
                    return FormatInstallResult(text, lowerCmd);
                case "start":
                case "startservice":
                    return FormatStartResult(text);
                case "capture":
                case "capturefb":
                    return FormatCaptureResult(text);
                case "trimimagecache":
                    return FormatSimpleAction(text, "图片内存缓存已处理");
                case "dumpmemory":
                    return FormatSimpleAction(text, "内存快照已导出到设备 /tmp/httpdump.snapshot");
                case "beginmonkey":
                case "stopmonkey":
                    return FormatSimpleAction(text, lowerCmd == "beginmonkey" ? "Monkey 测试已开始" : "Monkey 测试已停止");
                case "injectkey":
                    return FormatSimpleAction(text, "按键事件已注入");
                case "debugapp":
                case "debugservice":
                    return FormatSimpleAction(text, "调试目标已设置");
                case "setrenderconfig":
                    return FormatSimpleAction(text, "渲染配置已更新");
                default:
                    return FormatGeneric(text);
            }
        }

        private static string FormatUsageHelp(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【miniapp_cli 帮助】");
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (trimmed.StartsWith("miniapp_cli ", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("  " + trimmed);
                }
                else if (trimmed.StartsWith("--"))
                {
                    sb.AppendLine("    " + trimmed);
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatMemoryApp(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【进程内存分析】");

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Mallinfo", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(ParseMallinfo(trimmed));
                }
                else if (trimmed.StartsWith("App(", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(trimmed, @"App\((\d+)\)\s+size\(me\)=(\d+)\s+size\(all\)=(\d+)");
                    if (match.Success)
                    {
                        sb.AppendLine($"  · 应用 {match.Groups[1].Value}");
                        sb.AppendLine($"    自身内存: {FormatKb(long.Parse(match.Groups[2].Value))}");
                        sb.AppendLine($"    总内存:   {FormatKb(long.Parse(match.Groups[3].Value))}");
                    }
                    else
                    {
                        sb.AppendLine("  · " + trimmed);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trimmed) && trimmed != "MemoryApp:")
                {
                    sb.AppendLine("  · " + trimmed);
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string ParseMallinfo(string line)
        {
            var arena = Regex.Match(line, @"arena=(\d+)");
            var used = Regex.Match(line, @"uordblks=(\d+)");
            var free = Regex.Match(line, @"fordblks=(\d+)");
            var phase = line.IndexOf("before", StringComparison.OrdinalIgnoreCase) >= 0 ? "清理前" : "清理后";

            if (arena.Success && used.Success)
            {
                return $"  · 堆内存 ({phase}): 已用 {FormatBytes(long.Parse(used.Groups[1].Value))}" +
                       (free.Success ? $"，可用 {FormatBytes(long.Parse(free.Groups[1].Value))}" : "") +
                       $"，总量 {FormatBytes(long.Parse(arena.Groups[1].Value))}";
            }

            return "  · " + line;
        }

        private static string FormatQuickJsMemory(string text, bool afterGc)
        {
            var sb = new StringBuilder();
            sb.AppendLine(afterGc ? "【QuickJS 内存 · GC 后】" : "【QuickJS 内存】");

            var memoryMatch = Regex.Match(text, @"memory:\s*(\d+)", RegexOptions.IgnoreCase);
            var mallocMatch = Regex.Match(text, @"malloc:\s*(\d+)", RegexOptions.IgnoreCase);
            var objMatch = Regex.Match(text, @"(?:objects|obj\s*count):\s*(\d+)", RegexOptions.IgnoreCase);

            if (memoryMatch.Success)
            {
                sb.AppendLine($"  · JS 堆内存: {FormatBytes(long.Parse(memoryMatch.Groups[1].Value))}");
            }

            if (mallocMatch.Success)
            {
                sb.AppendLine($"  · 分配内存: {FormatBytes(long.Parse(mallocMatch.Groups[1].Value))}");
            }

            if (objMatch.Success)
            {
                sb.AppendLine($"  · 对象数量: {objMatch.Groups[1].Value}");
            }

            var hasStructured = memoryMatch.Success || mallocMatch.Success;
            if (!hasStructured)
            {
                foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    sb.AppendLine("  · " + line.Trim());
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatInstallResult(string text, string command)
        {
            var sb = new StringBuilder();
            sb.AppendLine(command == "install" ? "【安装结果】" : "【卸载结果】");
            sb.AppendLine(InterpretStatus(text));
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine("  · " + line.Trim());
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatStartResult(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【启动结果】");
            sb.AppendLine(InterpretStatus(text));
            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine("  · " + line.Trim());
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatCaptureResult(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【截图结果】");
            sb.AppendLine(InterpretStatus(text));

            var pathMatch = Regex.Match(text, @"(/[\w./-]+\.(?:png|jpg|bmp))", RegexOptions.IgnoreCase);
            if (pathMatch.Success)
            {
                sb.AppendLine($"  · 设备路径: {pathMatch.Groups[1].Value}");
            }

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sb.AppendLine("  · " + line.Trim());
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatSimpleAction(string text, string defaultSuccessHint)
        {
            var status = InterpretStatus(text);
            var sb = new StringBuilder();
            sb.AppendLine("【执行结果】");
            sb.AppendLine(status);

            if (status.IndexOf("成功", StringComparison.OrdinalIgnoreCase) >= 0
                && !string.IsNullOrWhiteSpace(defaultSuccessHint))
            {
                sb.AppendLine($"  · {defaultSuccessHint}");
            }

            foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine("  · " + line.Trim());
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatGeneric(string text)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【输出摘要】");
            sb.AppendLine(InterpretStatus(text));

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 8)
            {
                foreach (var line in lines)
                {
                    sb.AppendLine("  · " + line.Trim());
                }
            }
            else
            {
                foreach (var line in lines.Take(5))
                {
                    sb.AppendLine("  · " + line.Trim());
                }

                sb.AppendLine($"  · ... 共 {lines.Length} 行，详见原始输出");
            }

            return sb.ToString().TrimEnd();
        }

        private static string InterpretStatus(string text)
        {
            var parsed = MiniAppCliResultParser.Parse(text);
            if (parsed.HasJson && parsed.ReturnCode.HasValue)
            {
                return parsed.IsSuccess
                    ? "  状态: ✓ 执行成功"
                    : $"  状态: ✗ 失败 (ret={parsed.ReturnCode.Value})";
            }

            var lower = text.ToLowerInvariant();
            if (lower.IndexOf("error", StringComparison.Ordinal) >= 0
                || lower.IndexOf("fail", StringComparison.Ordinal) >= 0
                || lower.IndexOf("not found", StringComparison.Ordinal) >= 0)
            {
                return "  状态: ⚠ 可能失败，请查看详情";
            }

            if (lower.IndexOf("success", StringComparison.Ordinal) >= 0
                || lower.IndexOf("ok", StringComparison.Ordinal) >= 0
                || lower.IndexOf("done", StringComparison.Ordinal) >= 0)
            {
                return "  状态: ✓ 执行成功";
            }

            return "  状态: 已完成（请查看详情确认）";
        }

        private static string FormatKb(long kb) => kb >= 1024 ? $"{kb / 1024.0:F1} MB" : $"{kb} KB";

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
            }

            if (bytes >= 1024L * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }
    }
}
