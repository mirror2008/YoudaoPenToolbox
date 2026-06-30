using System;
using System.Text;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class AdbPersistService
    {
        public const string AuthFilePath = "/tmp/.adb_auth_verified";
        public const string SkipReBootScript = "/userdisk/skip_re/skip_login.sh";
        public const string UserdataPersistDir = "/userdata/adb_persist";
        public const string UserdiskPersistDir = "/userdisk/adb_persist";
        public const string UserdataBootScript = "/userdata/adb_persist/boot.sh";
        public const string UserdiskBootScript = "/userdisk/adb_persist/boot.sh";
        public const string UserdataAuthScript = "/userdata/adb_persist/adbd_auth.sh";
        public const string PersistMarker = "YOUDAO_PEN_TOOLBOX_ADB_PERSIST";

        public const string TouchLine = "touch /tmp/.adb_auth_verified >/dev/null 2>&1";
        public const string UserdiskBootHookLine =
            "[ -f /userdisk/adb_persist/boot.sh ] && sh /userdisk/adb_persist/boot.sh >/dev/null 2>&1";
        public const string UserdataBootHookLine =
            "[ -f /userdata/adb_persist/boot.sh ] && sh /userdata/adb_persist/boot.sh >/dev/null 2>&1";

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
                    && status.SkipReScriptHead.IndexOf(PersistMarker, StringComparison.Ordinal) >= 0
                    && status.SkipReScriptHead.IndexOf(".adb_auth_verified", StringComparison.Ordinal) >= 0;
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
                return new AdbPersistEnsureResult
                {
                    Action = AdbPersistEnsureAction.AlreadyEnabled,
                    Status = status
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

            if (!status.IsPersistEnabled)
            {
                return "未配置";
            }

            return status.AuthFileExists ? "已配置" : "已配置（重启后自动授权）";
        }

        public async Task<string> EnableAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                throw new InvalidOperationException("请先解锁 ADB 再来");
            }

            var log = new StringBuilder();
            log.AppendLine(await DeployPersistScriptsAsync(serial).ConfigureAwait(false));
            log.AppendLine(await InstallSkipReHookAsync(serial).ConfigureAwait(false));
            log.AppendLine(await ApplyImmediateAsync(serial).ConfigureAwait(false));
            return log.ToString().Trim();
        }

        public async Task<string> DisableAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                throw new InvalidOperationException("请先解锁 ADB 再来");
            }

            if (!status.SkipReHookInstalled && !status.SkipReScriptExists)
            {
                var bootExists = await _adbService.ShellAsync(serial,
                    $"test -f {UserdataBootScript} -o -f {UserdiskBootScript} && echo BOOT_YES || echo BOOT_NO")
                    .ConfigureAwait(false);
                if (bootExists == null || bootExists.IndexOf("BOOT_YES", StringComparison.Ordinal) < 0)
                {
                    return "未发现已安装的开机脚本钩子。";
                }
            }

            var log = new StringBuilder();
            log.AppendLine(await RemoveSkipReHookAsync(serial).ConfigureAwait(false));
            log.AppendLine(await RemovePersistScriptsAsync(serial).ConfigureAwait(false));
            log.AppendLine($"已保留授权验证文件 {AuthFilePath}（未删除）。");
            return log.ToString().Trim();
        }

        public Task<string> ApplyImmediateAsync(string serial)
        {
            return _adbService.ShellAsync(serial,
                $"sh {UserdiskBootScript} 2>/dev/null; sh {UserdataBootScript} 2>/dev/null; touch {AuthFilePath}; ls -la {AuthFilePath}");
        }

        public async Task<string> TestHookAsync(string serial)
        {
            var status = await GetStatusAsync(serial).ConfigureAwait(false);
            if (!status.ShellAccessible)
            {
                throw new InvalidOperationException("请先解锁 ADB 再来");
            }

            if (!status.SkipReHookInstalled)
            {
                throw new InvalidOperationException("skip_re 钩子尚未安装，请先启用持久化。");
            }

            var output = new StringBuilder();
            output.AppendLine("删除现有授权文件...");
            output.AppendLine(await _adbService.ShellAsync(serial, $"rm -f {AuthFilePath}").ConfigureAwait(false));
            output.AppendLine("模拟开机：执行 skip_login.sh ...");
            output.AppendLine(await _adbService.ShellAsync(serial,
                $"sh {SkipReBootScript} 2>&1").ConfigureAwait(false));
            output.AppendLine("检查授权文件...");
            output.AppendLine(await _adbService.ShellAsync(serial,
                $"ls -la {AuthFilePath} 2>&1").ConfigureAwait(false));
            return output.ToString().Trim();
        }

        public async Task<string> DiagnoseAsync(string serial)
        {
            var log = new StringBuilder();
            var status = await GetStatusAsync(serial).ConfigureAwait(false);

            log.AppendLine($"Shell 可用: {(status.ShellAccessible ? "是" : "否")}");
            log.AppendLine($"授权文件: {(status.AuthFileExists ? "存在" : "不存在")}");
            log.AppendLine($"skip_re 钩子: {(status.SkipReHookInstalled ? "已安装" : "未安装")}");
            log.AppendLine($"状态: {status.Summary}");

            if (!status.ShellAccessible)
            {
                log.AppendLine();
                log.AppendLine("PC 上先执行: adb shell auth （输入密码）");
                log.AppendLine("成功后再点「启用持久化」。");
                return log.ToString().Trim();
            }

            log.AppendLine();
            log.AppendLine("--- skip_login.sh 头部 ---");
            log.AppendLine((await _adbService.ShellAsync(serial, $"head -15 {SkipReBootScript} 2>&1").ConfigureAwait(false))?.Trim());
            log.AppendLine();
            log.AppendLine("--- /userdisk/adb_persist/ ---");
            log.AppendLine((await _adbService.ShellAsync(serial,
                $"ls -la {UserdiskPersistDir}/ 2>&1; echo ---; cat {UserdiskBootScript} 2>&1").ConfigureAwait(false))?.Trim());
            log.AppendLine();
            log.AppendLine("--- /userdata/adb_persist/ ---");
            log.AppendLine((await _adbService.ShellAsync(serial,
                $"ls -la {UserdataPersistDir}/ 2>&1; echo ---; cat {UserdataBootScript} 2>&1").ConfigureAwait(false))?.Trim());
            log.AppendLine();
            log.AppendLine("--- S99_run_test_scripts ---");
            log.AppendLine((await _adbService.ShellAsync(serial,
                "grep -n skip_login /etc/init.d/S99_run_test_scripts 2>/dev/null; head -25 /etc/init.d/S99_run_test_scripts 2>/dev/null")
                .ConfigureAwait(false))?.Trim());
            log.AppendLine();
            log.AppendLine("--- adbd_auth 绑定 ---");
            log.AppendLine((await _adbService.ShellAsync(serial,
                "mount | grep adbd_auth; ls -la /tmp/.adb_auth_verified 2>&1").ConfigureAwait(false))?.Trim());

            return log.ToString().Trim();
        }

        private async Task<string> DeployPersistScriptsAsync(string serial)
        {
            var authScript =
                "#!/bin/sh\n" +
                "# " + PersistMarker + "\n" +
                TouchLine + "\n" +
                "echo \"success.\"\n" +
                "exit 0\n";

            var bootScript =
                "#!/bin/sh\n" +
                "# " + PersistMarker + "\n" +
                TouchLine + "\n" +
                "if [ -f /userdata/adb_persist/adbd_auth.sh ]; then\n" +
                "    mount --bind /userdata/adb_persist/adbd_auth.sh /usr/bin/adbd_auth.sh 2>/dev/null\n" +
                "fi\n";

            var deployCmd =
                "mkdir -p /userdata/adb_persist /userdisk/adb_persist /userdisk/skip_re; " +
                "printf '%s' '" + EscapeForShell(authScript) + "' > /userdata/adb_persist/adbd_auth.sh; " +
                "printf '%s' '" + EscapeForShell(bootScript) + "' > /userdata/adb_persist/boot.sh; " +
                "printf '%s' '" + EscapeForShell(bootScript) + "' > /userdisk/adb_persist/boot.sh; " +
                "chmod +x /userdata/adb_persist/adbd_auth.sh /userdata/adb_persist/boot.sh /userdisk/adb_persist/boot.sh; " +
                "ls -la /userdata/adb_persist/ /userdisk/adb_persist/";

            var result = await _adbService.ShellAsync(serial, deployCmd).ConfigureAwait(false);
            return "持久化脚本已部署（userdata + userdisk）:\n" + result?.Trim();
        }

        private async Task<string> InstallSkipReHookAsync(string serial)
        {
            var script =
                "mkdir -p /userdisk/skip_re; " +
                "if [ ! -f /userdisk/skip_re/skip_login.sh ]; then " +
                "  printf '%s\\n' '# " + PersistMarker + "' '" + TouchLine + "' '" + UserdiskBootHookLine + "' '" + UserdataBootHookLine + "' > /userdisk/skip_re/skip_login.sh; " +
                "else " +
                "  if ! grep -q '" + PersistMarker + "' /userdisk/skip_re/skip_login.sh 2>/dev/null; then " +
                "    TMP=/tmp/ypt_skip_$$; " +
                "    { printf '%s\\n' '# " + PersistMarker + "' '" + TouchLine + "' '" + UserdiskBootHookLine + "' '" + UserdataBootHookLine + "'; cat /userdisk/skip_re/skip_login.sh; } > \"$TMP\" && mv \"$TMP\" /userdisk/skip_re/skip_login.sh; " +
                "  elif ! grep -q '.adb_auth_verified' /userdisk/skip_re/skip_login.sh 2>/dev/null; then " +
                "    TMP=/tmp/ypt_skip_$$; " +
                "    { printf '%s\\n' '# " + PersistMarker + "' '" + TouchLine + "' '" + UserdiskBootHookLine + "' '" + UserdataBootHookLine + "'; cat /userdisk/skip_re/skip_login.sh; } > \"$TMP\" && mv \"$TMP\" /userdisk/skip_re/skip_login.sh; " +
                "  fi; " +
                "fi; " +
                "chmod +x /userdisk/skip_re/skip_login.sh; " +
                "head -12 /userdisk/skip_re/skip_login.sh";

            var result = await _adbService.ShellAsync(serial, script).ConfigureAwait(false);

            var verify = await _adbService.ShellAsync(serial,
                $"grep -q '{PersistMarker}' {SkipReBootScript} && grep -q '.adb_auth_verified' {SkipReBootScript} && echo HOOK_OK || echo HOOK_FAIL")
                .ConfigureAwait(false);

            if (verify == null || verify.IndexOf("HOOK_OK", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("写入 skip_re 开机钩子失败。");
            }

            return "skip_re 开机钩子已写入（S99_run_test_scripts 执行）:\n" + result?.Trim();
        }

        private async Task<string> RemoveSkipReHookAsync(string serial)
        {
            var script =
                "if [ -f /userdisk/skip_re/skip_login.sh ]; then " +
                "  grep -q '" + PersistMarker + "' /userdisk/skip_re/skip_login.sh 2>/dev/null || exit 0; " +
                "  sed -i '/" + PersistMarker + "/d' /userdisk/skip_re/skip_login.sh; " +
                "  sed -i '/\\.adb_auth_verified/d' /userdisk/skip_re/skip_login.sh; " +
                "  sed -i '/userdata\\/adb_persist\\/boot.sh/d' /userdisk/skip_re/skip_login.sh; " +
                "  sed -i '/userdisk\\/adb_persist\\/boot.sh/d' /userdisk/skip_re/skip_login.sh; " +
                "  sed -i '/^$/N;/^\\n$/D' /userdisk/skip_re/skip_login.sh; " +
                "  head -12 /userdisk/skip_re/skip_login.sh; " +
                "fi";

            var result = await _adbService.ShellAsync(serial, script).ConfigureAwait(false);
            return "skip_re 开机钩子已移除:\n" + result?.Trim();
        }

        private async Task<string> RemovePersistScriptsAsync(string serial)
        {
            var script =
                "umount /usr/bin/adbd_auth.sh 2>/dev/null; " +
                "rm -f /userdata/adb_persist/boot.sh /userdata/adb_persist/adbd_auth.sh; " +
                "rm -f /userdisk/adb_persist/boot.sh; " +
                "rmdir /userdata/adb_persist /userdisk/adb_persist 2>/dev/null; " +
                "echo userdata:; ls -la /userdata/adb_persist/ 2>&1; echo userdisk:; ls -la /userdisk/adb_persist/ 2>&1";

            var result = await _adbService.ShellAsync(serial, script).ConfigureAwait(false);
            return result?.Trim();
        }

        private static string EscapeForShell(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("'", "'\\''")
                .Replace("\r", string.Empty)
                .Replace("\n", "\\n");
        }
    }
}
