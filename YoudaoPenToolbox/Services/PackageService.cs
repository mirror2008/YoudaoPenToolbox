using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class PackageService
    {
        private readonly AdbService _adbService;

        public string LastError { get; private set; }

        private static readonly string[] PackageJsonPaths =
        {
            "/data/miniapp/data/mini_app/pkg/packages.json",
            "/userdata/miniapp/data/mini_app/pkg/packages.json",
            "/userdisk/secondary/miniapp/data/mini_app/pkg/packages.json",
            "/userdisk/miniapp/data/mini_app/pkg/packages.json"
        };

        private static readonly string[] PackageDirRoots =
        {
            "/data/miniapp/data/mini_app/pkg",
            "/userdisk/secondary/miniapp/data/mini_app/pkg",
            "/userdisk/miniapp/data/mini_app/pkg",
            "/userdata/miniapp/data/mini_app/pkg"
        };

        public PackageService(AdbService adbService)
        {
            _adbService = adbService;
        }

        public async Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(string serial, bool includeSizes = true)
        {
            LastError = null;

            foreach (var path in PackageJsonPaths)
            {
                try
                {
                    var json = await _adbService.ReadRemoteTextFileAsync(serial, path).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    var root = JObject.Parse(json);
                    var packages = root["packages"] as JArray;
                    if (packages == null)
                    {
                        LastError = $"packages.json 格式无效: {path}";
                        continue;
                    }

                    var apps = packages.Select(ParseApp).Where(a => a != null).ToList();

                    if (includeSizes)
                    {
                        try
                        {
                            await FillAppSizesAsync(serial, apps).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LastError = $"应用列表已加载，但计算占用失败: {ex.Message}";
                        }
                    }

                    if (apps.Count > 0)
                    {
                        return apps.OrderByDescending(a => a.SizeKb).ThenBy(a => a.Name).ToList();
                    }
                }
                catch (Exception ex)
                {
                    LastError = $"读取 {path} 失败: {ex.Message}";
                }
            }

            return Array.Empty<InstalledApp>();
        }

        public async Task<string> GetInstallPathAsync(string serial, string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return null;
            }

            foreach (var path in PackageJsonPaths)
            {
                try
                {
                    var json = await _adbService.ReadRemoteTextFileAsync(serial, path).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    var root = JObject.Parse(json);
                    var packages = root["packages"] as JArray;
                    if (packages == null)
                    {
                        continue;
                    }

                    foreach (var entry in packages)
                    {
                        if (!string.Equals(entry?["appid"]?.ToString(), appId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var installPath = entry["installPath"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(installPath))
                        {
                            return NormalizePath(installPath);
                        }

                        var packageDir = entry["packageDir"]?.ToString();
                        if (string.IsNullOrWhiteSpace(packageDir))
                        {
                            return null;
                        }

                        var useBSlot = ReadBool(entry["b"]) == true;
                        return NormalizePath($"{packageDir.TrimEnd('/')}/{(useBSlot ? "b" : "a")}");
                    }
                }
                catch
                {

                }
            }

            return null;
        }

        public async Task<long> GetAppSizeAsync(string serial, InstalledApp app)
        {
            if (app == null)
            {
                return 0;
            }

            var target = !string.IsNullOrWhiteSpace(app.PackageDir)
                ? app.PackageDir.TrimEnd('/')
                : $"{PackageDirRoots[0]}/{app.AppId}";

            var output = await _adbService.ShellAsync(serial, $"du -sk \"{target}\" 2>/dev/null").ConfigureAwait(false);
            var map = ParseDuOutput(output);
            return map.TryGetValue(NormalizePath(target), out var size) ? size : 0;
        }

        private static InstalledApp ParseApp(JToken p)
        {
            if (p == null || p.Type != JTokenType.Object)
            {
                return null;
            }

            var appId = p["appid"]?.ToString();
            if (string.IsNullOrWhiteSpace(appId))
            {
                return null;
            }

            var flag = ReadInt(p["flag"]);
            var props = p["props"] as JObject;
            bool? supportUninstall = null;
            if (props != null)
            {
                supportUninstall = ReadBool(props["supportUnInstall"]);
            }

            var app = new InstalledApp
            {
                AppId = appId,
                Name = p["name"]?.ToString() ?? "未知",
                Version = p["version"]?.ToString() ?? "?",
                Category = p["category"]?.ToString() ?? "",
                PackageDir = p["packageDir"]?.ToString(),
                InstallPath = p["installPath"]?.ToString(),
                IsThirdParty = flag == 16384,
                CanUninstall = supportUninstall ?? (flag == 16384)
            };

            ProtectedSystemAppPolicy.Apply(app);
            return app;
        }

        private static int ReadInt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return 0;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<int>();
            }

            return int.TryParse(token.ToString(), out var val) ? val : 0;
        }

        private static bool? ReadBool(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            if (bool.TryParse(token.ToString(), out var val))
            {
                return val;
            }

            return null;
        }

        private async Task FillAppSizesAsync(string serial, IList<InstalledApp> apps)
        {
            var duArgs = string.Join(" ", PackageDirRoots.Select(r => $"{r}/*/"));
            var output = await _adbService.ShellAsync(serial, $"du -sk {duArgs} 2>/dev/null").ConfigureAwait(false);
            var sizeMap = ParseDuOutput(output);

            foreach (var app in apps)
            {
                if (!string.IsNullOrWhiteSpace(app.PackageDir)
                    && sizeMap.TryGetValue(NormalizePath(app.PackageDir), out var size))
                {
                    app.SizeKb = size;
                    continue;
                }

                var fallbackKey = PackageDirRoots
                    .Select(root => NormalizePath($"{root}/{app.AppId}"))
                    .FirstOrDefault(sizeMap.ContainsKey);

                if (fallbackKey != null)
                {
                    app.SizeKb = sizeMap[fallbackKey];
                }
            }
        }

        private static Dictionary<string, long> ParseDuOutput(string output)
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(output))
            {
                return map;
            }

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(line.Trim(), @"^(\d+)\s+(.+)$");
                if (!match.Success)
                {
                    continue;
                }

                if (!long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
                {
                    continue;
                }

                map[NormalizePath(match.Groups[2].Value)] = kb;
            }

            return map;
        }

        private static string NormalizePath(string path)
        {
            return path.Trim().Replace('\\', '/').TrimEnd('/');
        }
    }
}
