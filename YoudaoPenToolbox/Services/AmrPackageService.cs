using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public static class AmrPackageService
    {
        private static readonly string[] IconCandidates =
        {
            "ios_icon.png",
            "app_icon.png",
            "appicon.png",
            "logo.png"
        };

        public static AmrPackageInfo Parse(string filePath)
        {
            var info = new AmrPackageInfo
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                AppName = Path.GetFileNameWithoutExtension(filePath),
                Version = "-",
                AppId = "-"
            };

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                info.ErrorMessage = "文件不存在";
                return info;
            }

            var fileInfo = new FileInfo(filePath);
            info.FileSizeBytes = fileInfo.Length;

            try
            {
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    var manifestEntry = archive.GetEntry("manifest.json")
                        ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));

                    if (manifestEntry == null)
                    {
                        info.ErrorMessage = "未找到 manifest.json";
                        info.IsValid = true;
                        return info;
                    }

                    string manifestJson;
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        manifestJson = reader.ReadToEnd();
                    }

                    var manifest = JObject.Parse(manifestJson);
                    info.AppName = FirstNonEmpty(
                        manifest["appName"]?.ToString(),
                        manifest["name"]?.ToString(),
                        info.AppName);
                    info.Version = FirstNonEmpty(
                        manifest["version"]?.ToString(),
                        ParseVersionFromFileName(info.FileName),
                        "-");
                    info.AppId = FirstNonEmpty(
                        manifest["appid"]?.ToString(),
                        manifest["appId"]?.ToString(),
                        "-");

                    var iconName = manifest["icon"]?.ToString();
                    var iconEntry = FindIconEntry(archive, iconName);
                    if (iconEntry != null)
                    {
                        info.Icon = ExtractIcon(iconEntry, info);
                    }

                    info.IsValid = true;
                    return info;
                }
            }
            catch (InvalidDataException)
            {
                info.ErrorMessage = "不是有效的 AMR/ZIP 安装包";
                return info;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"解析失败: {ex.Message}";
                info.IsValid = true;
                return info;
            }
        }

        private static ZipArchiveEntry FindIconEntry(ZipArchive archive, string iconName)
        {
            if (!string.IsNullOrWhiteSpace(iconName))
            {
                var entry = archive.GetEntry(iconName)
                    ?? archive.Entries.FirstOrDefault(e =>
                        e.FullName.Equals(iconName, StringComparison.OrdinalIgnoreCase)
                        || e.FullName.EndsWith("/" + iconName, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    return entry;
                }
            }

            foreach (var candidate in IconCandidates)
            {
                var entry = archive.GetEntry(candidate)
                    ?? archive.Entries.FirstOrDefault(e =>
                        e.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    return entry;
                }
            }

            return archive.Entries.FirstOrDefault(e =>
                e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && e.Name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static BitmapImage ExtractIcon(ZipArchiveEntry iconEntry, AmrPackageInfo info)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), "youdao_amr_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            var tempIconPath = Path.Combine(tempFolder, Path.GetFileName(iconEntry.FullName));
            iconEntry.ExtractToFile(tempIconPath, true);
            info.SetTempPaths(tempFolder, tempIconPath);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(tempIconPath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "-";
        }

        private static string ParseVersionFromFileName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            {
                var versionPart = name.Substring(lastUnderscore + 1);
                if (versionPart.Length > 0 && char.IsDigit(versionPart[0]))
                {
                    return versionPart.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                        ? versionPart.Substring(1)
                        : versionPart;
                }
            }

            return null;
        }
    }
}
