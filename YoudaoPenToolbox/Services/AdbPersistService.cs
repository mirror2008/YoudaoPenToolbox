using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    /// <summary>
    /// Install / manage the on-device helper miniapp which owns session keep-alive.
    /// </summary>
    public class AdbPersistService
    {
        public const string HelperAppId = "8001888662037901";
        public const string HelperAppName = "有道工具箱辅助助手";
        public const string AuthFilePath = "/tmp/.adb_auth_verified";
        public const string RemoteAmrPath = "/userdisk/yd_toolbox_helper.amr";
        public const string HelperMarker = "YD_TOOLBOX_HELPER";
        public const string SkipReBootScript = "/userdisk/skip_re/skip_login.sh";

        private const string EmbeddedResourceName = "embedded.helper.helper.amr";
        private const string GiteeVersionUrl =
            "https://gitee.com/yanda2008/penmirror/raw/master/UPDATE/assistant/version.json";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        private readonly AdbService _adbService;

        public AdbPersistService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public async Task<AdbPersistStatus> GetStatusAsync(string serial)
        {
            var status = new AdbPersistStatus();

            var probe = await _adbService.ShellAsync(serial, "echo toolbox_probe_ok").ConfigureAwait(false);
            status.ShellAccessible = probe != null
                && probe.IndexOf("toolbox_probe_ok", StringComparison.Ordinal) >= 0;

            if (!status.ShellAccessible)
            {
                status.Summary = "请先解锁 ADB 再来";
                return status;
            }

            var detect = await _adbService.ShellAsync(serial,
                    "AID=" + HelperAppId + "; " +
                    "FOUND=; VER=; " +
                    "for root in " +
                    "/userdisk/secondary/miniapp/data/mini_app/pkg " +
                    "/userdisk/miniapp/data/mini_app/pkg " +
                    "/userdata/miniapp/data/mini_app/pkg " +
                    "/data/miniapp/data/mini_app/pkg; do " +
                    "  if [ -d \"$root/$AID\" ]; then FOUND=1; " +
                    "    mf=$(ls \"$root/$AID\"/*/manifest.json 2>/dev/null | head -1); " +
                    "    if [ -n \"$mf\" ]; then " +
                    "      VER=$(grep -o '\"version\"[[:space:]]*:[[:space:]]*\"[^\"]*\"' \"$mf\" 2>/dev/null | head -1); " +
                    "    fi; " +
                    "    break; " +
                    "  fi; " +
                    "done; " +
                    "if [ -z \"$FOUND\" ]; then " +
                    "  for pj in " +
                    "/userdisk/secondary/miniapp/data/mini_app/pkg/packages.json " +
                    "/userdisk/miniapp/data/mini_app/pkg/packages.json " +
                    "/userdata/miniapp/data/mini_app/pkg/packages.json " +
                    "/data/miniapp/data/mini_app/pkg/packages.json; do " +
                    "    if grep -q \"$AID\" \"$pj\" 2>/dev/null; then FOUND=1; break; fi; " +
                    "  done; " +
                    "fi; " +
                    "if [ -n \"$FOUND\" ]; then echo HELPER_YES; else echo HELPER_NO; fi; " +
                    "echo VER_LINE:$VER")
                .ConfigureAwait(false);

            status.HelperInstalled = detect != null
                && detect.IndexOf("HELPER_YES", StringComparison.Ordinal) >= 0;
            status.HelperVersion = ExtractJsonStringValue(
                ExtractAfterMarker(detect, "VER_LINE:"), "version");

            var authCheck = await _adbService.ShellAsync(serial,
                $"test -f {AuthFilePath} && echo AUTH_YES || echo AUTH_NO").ConfigureAwait(false);
            status.AuthFileExists = authCheck != null && authCheck.IndexOf("AUTH_YES", StringComparison.Ordinal) >= 0;

            var skipExists = await _adbService.ShellAsync(serial,
                $"test -f {SkipReBootScript} && echo SKIP_YES || echo SKIP_NO").ConfigureAwait(false);
            status.SkipReScriptExists = skipExists != null && skipExists.IndexOf("SKIP_YES", StringComparison.Ordinal) >= 0;
            if (status.SkipReScriptExists)
            {
                status.SkipReScriptHead = await _adbService.ShellAsync(serial,
                    $"head -12 {SkipReBootScript}").ConfigureAwait(false);
                status.SkipReHookInstalled = status.SkipReScriptHead != null
                    && status.SkipReScriptHead.IndexOf(HelperMarker, StringComparison.Ordinal) >= 0;
            }

            status.Summary = BuildSummary(status);
            return status;
        }

        public async Task<AdbPersistEnsureResult> EnsurePersistAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                return new AdbPersistEnsureResult
                {
                    Action = AdbPersistEnsureAction.SkippedShellLocked,
                    Status = status
                };
            }

            if (status.IsPersistEnabled)
            {
                // Re-start helper so it refreshes session
                await StartHelperAsync(serial).ConfigureAwait(false);
                var after = await GetStatusAsync(serial).ConfigureAwait(false);
                return new AdbPersistEnsureResult
                {
                    Action = AdbPersistEnsureAction.AlreadyEnabled,
                    Status = after
                };
            }

            try
            {
                var log = await EnableAsync(serial).ConfigureAwait(false);
                var after = await GetStatusAsync(serial).ConfigureAwait(false);
                return new AdbPersistEnsureResult
                {
                    Action = AdbPersistEnsureAction.Configured,
                    Status = after,
                    Log = log
                };
            }
            catch (Exception ex)
            {
                return new AdbPersistEnsureResult
                {
                    Action = AdbPersistEnsureAction.Failed,
                    Status = status,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static string BuildSummary(AdbPersistStatus status)
        {
            if (!status.ShellAccessible)
            {
                return "请先解锁 ADB 再来";
            }

            if (!status.HelperInstalled)
            {
                return "未安装辅助助手";
            }

            var ver = string.IsNullOrWhiteSpace(status.HelperVersion) ? "" : (" v" + status.HelperVersion);
            return status.AuthFileExists
                ? "辅助助手已就绪" + ver
                : "辅助助手已安装" + ver;
        }

        public async Task<string> EnableAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                throw new InvalidOperationException("请先解锁 ADB 再来");
            }

            var log = new StringBuilder();
            var localAmr = await ResolveHelperAmrAsync(log).ConfigureAwait(false);
            log.AppendLine("本地包: " + localAmr);

            var pushed = await _adbService.PushFileAsync(serial, localAmr, RemoteAmrPath).ConfigureAwait(false);
            if (!pushed)
            {
                throw new InvalidOperationException("推送辅助助手安装包失败");
            }
            log.AppendLine("已推送到 " + RemoteAmrPath);

            var installOut = await _adbService.ShellAsync(serial,
                "miniapp_cli install " + RemoteAmrPath + " 2>&1; echo EXIT:$?").ConfigureAwait(false);
            log.AppendLine(installOut?.Trim());
            if (installOut == null || installOut.IndexOf("EXIT:0", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("安装辅助助手失败");
            }

            log.AppendLine(await StartHelperAsync(serial).ConfigureAwait(false));
            await Task.Delay(1200).ConfigureAwait(false);

            // Soft touch as belt-and-suspenders before helper finishes bootstrapping
            await _adbService.ShellAsync(serial,
                "mkdir -p /tmp; touch " + AuthFilePath + "; start adbd 2>/dev/null; echo TOUCH_OK")
                .ConfigureAwait(false);

            var after = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!after.HelperInstalled)
            {
                throw new InvalidOperationException("安装后未检测到辅助助手");
            }

            log.AppendLine("状态: " + after.Summary);
            return log.ToString().Trim();
        }

        public async Task<string> DisableAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                throw new InvalidOperationException("请先解锁 ADB 再来");
            }

            var log = new StringBuilder();
            var uninstall = await _adbService.ShellAsync(serial,
                "miniapp_cli uninstall " + HelperAppId + " 2>&1; echo EXIT:$?").ConfigureAwait(false);
            log.AppendLine(uninstall?.Trim());
            log.AppendLine(await RemoveHelperHooksAsync(serial).ConfigureAwait(false));
            return log.ToString().Trim();
        }

        public Task<string> ApplyImmediateAsync(string serial)
        {
            return StartHelperAsync(serial);
        }

        public async Task<string> TestHookAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                throw new InvalidOperationException("请先解锁 ADB 再来");
            }

            if (!status.HelperInstalled)
            {
                throw new InvalidOperationException("请先安装辅助助手");
            }

            var output = new StringBuilder();
            output.AppendLine("清除标记后重启助手...");
            output.AppendLine(await _adbService.ShellAsync(serial, "rm -f " + AuthFilePath).ConfigureAwait(false));
            output.AppendLine(await StartHelperAsync(serial).ConfigureAwait(false));
            await Task.Delay(1500).ConfigureAwait(false);
            output.AppendLine(await _adbService.ShellAsync(serial,
                "ls -la " + AuthFilePath + " 2>&1; pgrep adbd | head -3").ConfigureAwait(false));
            return output.ToString().Trim();
        }

        public async Task<string> DiagnoseAsync(string serial)
        {
            var log = new StringBuilder();
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            log.AppendLine($"Shell 可用: {(status.ShellAccessible ? "是" : "否")}");
            log.AppendLine($"辅助助手: {(status.HelperInstalled ? "已安装" : "未安装")} {status.HelperVersion}");
            log.AppendLine($"状态: {status.Summary}");
            log.AppendLine();
            log.AppendLine("--- 包目录 ---");
            log.AppendLine((await _adbService.ShellAsync(serial,
                "ls -la /userdisk/secondary/miniapp/data/mini_app/pkg/" + HelperAppId + " 2>&1")
                .ConfigureAwait(false))?.Trim());
            log.AppendLine();
            log.AppendLine("--- 标记 ---");
            log.AppendLine((await _adbService.ShellAsync(serial,
                "ls -la " + AuthFilePath + " 2>&1; pgrep adbd | head")
                .ConfigureAwait(false))?.Trim());
            return log.ToString().Trim();
        }

        private async Task<string> StartHelperAsync(string serial)
        {
            var outText = await _adbService.ShellAsync(serial,
                "miniapp_cli start " + HelperAppId + " 2>&1; echo START_EXIT:$?").ConfigureAwait(false);
            return "启动助手:\n" + (outText ?? "").Trim();
        }

        private async Task<string> ResolveHelperAmrAsync(StringBuilder log)
        {
            // Prefer Gitee force package when reachable
            try
            {
                var json = (await HttpClient.GetStringAsync(GiteeVersionUrl).ConfigureAwait(false)).Trim();
                var meta = JObject.Parse(json);
                var url = meta["url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var cacheDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "helper");
                    Directory.CreateDirectory(cacheDir);
                    var local = Path.Combine(cacheDir, "helper.amr");
                    using (var resp = await HttpClient.GetAsync(url).ConfigureAwait(false))
                    {
                        resp.EnsureSuccessStatusCode();
                        var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        File.WriteAllBytes(local, bytes);
                    }

                    if (new FileInfo(local).Length > 50000)
                    {
                        log.AppendLine("已从 Gitee 拉取辅助助手包");
                        return local;
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("Gitee 拉取跳过: " + ex.Message);
            }

            return ExtractEmbeddedHelperAmr();
        }

        private static string ExtractEmbeddedHelperAmr()
        {
            var cacheDir = Path.Combine(Path.GetTempPath(), "YoudaoPenToolbox", "helper");
            Directory.CreateDirectory(cacheDir);
            var local = Path.Combine(cacheDir, "helper_embedded.amr");
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(EmbeddedResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("未找到内置辅助助手安装包");
                }

                using (var fs = File.Create(local))
                {
                    stream.CopyTo(fs);
                }
            }

            if (!File.Exists(local) || new FileInfo(local).Length < 50000)
            {
                throw new InvalidOperationException("内置辅助助手安装包无效");
            }

            return local;
        }

        private async Task<string> RemoveHelperHooksAsync(string serial)
        {
            var script =
                "if [ -f " + SkipReBootScript + " ]; then " +
                "  sed -i '/" + HelperMarker + "/d' " + SkipReBootScript + "; " +
                "fi; " +
                "umount /usr/bin/adbd_auth.sh 2>/dev/null; " +
                "rm -f /userdata/adb_persist/boot.sh /userdata/adb_persist/adbd_auth.sh /userdisk/adb_persist/boot.sh; " +
                "echo CLEAN_OK";
            return (await _adbService.ShellAsync(serial, script).ConfigureAwait(false))?.Trim();
        }

        private static string ExtractJsonStringValue(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var marker = "\"" + key + "\"";
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return "";
            var colon = text.IndexOf(':', idx);
            if (colon < 0) return "";
            var q1 = text.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            var q2 = text.IndexOf('"', q1 + 1);
            if (q2 <= q1) return "";
            return text.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string ExtractAfterMarker(string text, string marker)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(marker)) return "";
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return "";
            var start = idx + marker.Length;
            var end = text.IndexOfAny(new[] { '\r', '\n' }, start);
            if (end < 0) end = text.Length;
            return text.Substring(start, end - start).Trim();
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YoudaoPenToolbox/helper");
            return client;
        }
    }
}
