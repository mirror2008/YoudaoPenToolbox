using System.Collections.Generic;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class MiniAppCliService
    {
        private readonly AdbService _adbService;

        public MiniAppCliService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public IReadOnlyList<MiniAppCommand> GetAvailableCommands()
        {
            return new List<MiniAppCommand>
            {
                Cmd("memoryApp", "内存调试", "显示进程内存使用",
                    "miniapp_cli memoryApp",
                    "查看 miniapp 进程及各子应用的内存占用，可用于排查内存泄漏。"),

                Cmd("trimImageCache", "内存调试", "清理图片内存缓存",
                    "miniapp_cli trimImageCache",
                    "释放图片解码缓存，内存紧张时可尝试。"),

                Cmd("debugApp", "内存调试", "设置调试 App",
                    "miniapp_cli debugApp {0}",
                    "为 memoryUsage / dumpMemory / devtool 指定要调试的应用。",
                    P("appid", "应用 ID", "16 位数字 AppId", "8001687764241341", "8080252464522508")),

                Cmd("debugService", "内存调试", "设置调试 Service",
                    "miniapp_cli debugService {0}",
                    "为内存调试命令指定 Service 类型的应用。",
                    P("appid", "Service AppId", "服务应用的 AppId", "8001650599023931", "8001650599023931")),

                Cmd("memoryUsage", "内存调试", "QuickJS 内存使用",
                    "miniapp_cli memoryUsage",
                    "显示当前 QuickJS 引擎内存占用（需先用 debugApp 指定目标）。"),

                Cmd("memoryUsageGC", "内存调试", "GC 后 QuickJS 内存",
                    "miniapp_cli memoryUsageGC",
                    "触发 GC 后显示 QuickJS 内存，便于对比 GC 效果。"),

                Cmd("dumpMemory", "内存调试", "导出内存快照",
                    "miniapp_cli dumpMemory",
                    "将 QuickJS 内存 dump 到设备 /tmp/httpdump.snapshot，用于深度分析。"),

                Cmd("install", "应用管理", "安装 AMR 包",
                    "miniapp_cli install {0}",
                    "安装小程序包到设备。建议先将 .amr 上传到 /userdisk/ 再安装。",
                    P("amrPath", "设备上 AMR 路径", "AMR 在设备上的绝对路径", "/userdisk/loli_v1.11.8.amr", "/userdisk/app.amr")),

                Cmd("uninstall", "应用管理", "卸载应用",
                    "miniapp_cli uninstall {0}",
                    "按 AppId 卸载已安装的小程序。系统内置应用可能无法卸载。",
                    P("appid", "应用 ID", "要卸载的应用 AppId", "8001687764241341", "8001687764241341")),

                Cmd("start", "应用管理", "启动应用",
                    "miniapp_cli start {0} --{1}",
                    "启动指定小程序并打开页面。page 参数不需要写 -- 前缀。",
                    P("appId", "应用 ID", "要启动的应用 AppId", "8001687764241341", "8001687764241341"),
                    P("page", "页面路径", "页面路由，通常为 index", "index", "index")),

                Cmd("startService", "应用管理", "启动应用服务",
                    "miniapp_cli startService {0} {1}",
                    "启动应用的 Background Service。",
                    P("appId", "应用 ID", "宿主应用 AppId", "8001650599023931", "8001650599023931"),
                    P("service", "服务名", "服务标识符", "player", "service")),

                Cmd("capture", "屏幕工具", "截取屏幕",
                    "miniapp_cli capture {0}",
                    "截取当前屏幕保存到设备指定路径。可用 adb pull 将图片拉取到电脑。",
                    P("path", "保存路径", "设备上的 PNG 路径", "/tmp/screen.png", "/tmp/capture.png")),

                Cmd("captureFB", "屏幕工具", "截取 Framebuffer",
                    "miniapp_cli captureFB {0}",
                    "直接从 fbdev 截屏，部分场景比 capture 更底层。",
                    P("path", "保存路径", "设备上的 PNG 路径", "/tmp/fb.png", "/tmp/capturefb.png")),

                Cmd("beginMonkey", "测试", "开始 Monkey 测试",
                    "miniapp_cli beginMonkey",
                    "启动随机压力测试（需设备端已启用）。谨慎使用，可能导致应用异常。"),

                Cmd("stopMonkey", "测试", "停止 Monkey 测试",
                    "miniapp_cli stopMonkey",
                    "停止正在运行的 Monkey 测试。"),

                Cmd("injectKey", "输入模拟", "注入按键",
                    "miniapp_cli injectKey {0}",
                    "向设备注入按键事件。例如 3=Home, 4=Back, 82=Menu。",
                    P("keyCode", "键值码", "Android KeyCode 数值", "4", "3")),

                Cmd("setRenderConfig", "系统配置", "设置渲染配置",
                    "miniapp_cli setRenderConfig {0} {1}",
                    "修改渲染引擎配置项。",
                    P("key", "配置键", "渲染配置 key", "fps", "debug"),
                    P("value", "配置值", "对应的值", "60", "1"))
            };
        }

        private static MiniAppCommand Cmd(string name, string category, string desc, string template, string notes,
            MiniAppParameter p1 = null, MiniAppParameter p2 = null)
        {
            MiniAppParameter[] parameters = null;
            if (p1 != null && p2 != null)
            {
                parameters = new[] { p1, p2 };
            }
            else if (p1 != null)
            {
                parameters = new[] { p1 };
            }

            var usage = template;
            if (p1 != null)
            {
                usage = usage.Replace("{0}", p1.Example ?? "{" + p1.Name + "}");
            }

            if (p2 != null)
            {
                usage = usage.Replace("{1}", p2.Example ?? "{" + p2.Name + "}");
            }

            return new MiniAppCommand
            {
                Name = name,
                Category = category,
                Description = desc,
                Usage = usage,
                Notes = notes,
                CommandTemplate = template,
                ParameterDetails = parameters
            };
        }

        private static MiniAppParameter P(string name, string label, string hint, string placeholder, string example)
        {
            return new MiniAppParameter
            {
                Name = name,
                Label = label,
                Hint = hint,
                Placeholder = placeholder,
                Example = example
            };
        }

        public async Task<string> ExecuteAsync(string serial, MiniAppCommand command, params string[] args)
        {
            if (command == null)
            {
                throw new System.ArgumentNullException(nameof(command));
            }

            string shellCommand;
            if (command.RequiresParameters)
            {
                if (args == null || args.Length < command.ParameterDetails.Length)
                {
                    throw new System.ArgumentException($"命令 {command.Name} 需要参数: {string.Join(", ", command.Parameters)}");
                }

                shellCommand = string.Format(command.CommandTemplate, args);
            }
            else
            {
                shellCommand = command.CommandTemplate;
            }

            return await _adbService.ShellAsync(serial, shellCommand).ConfigureAwait(false);
        }

        public Task<string> ExecuteRawAsync(string serial, string rawCommand, int timeoutMs = 120000)
        {
            var command = rawCommand?.Trim() ?? string.Empty;
            if (!command.StartsWith("miniapp_cli", System.StringComparison.OrdinalIgnoreCase))
            {
                command = "miniapp_cli " + command;
            }

            return _adbService.ShellAsync(serial, command, timeoutMs);
        }
    }
}
