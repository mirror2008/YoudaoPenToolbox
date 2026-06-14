using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace YoudaoPenToolbox.Services
{
    public class EmbeddedRuntimeBootstrapService
    {
        private const string ResourcePrefix = "embedded.tools.";

        private static readonly EmbeddedTool[] Tools =
        {
            new EmbeddedTool("adb.exe", ResourcePrefix + "adb.exe"),
            new EmbeddedTool("AdbWinApi.dll", ResourcePrefix + "AdbWinApi.dll"),
            new EmbeddedTool("AdbWinUsbApi.dll", ResourcePrefix + "AdbWinUsbApi.dll")
        };

        public string ToolsDirectory => AppDomain.CurrentDomain.BaseDirectory;

        public Task EnsureToolsAsync(IProgress<string> status, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => EnsureTools(status, cancellationToken), cancellationToken);
        }

        private void EnsureTools(IProgress<string> status, CancellationToken cancellationToken)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var missing = Tools.Where(tool => !IsValidFile(GetLocalPath(tool.FileName))).ToList();

            if (missing.Count == 0)
            {
                status?.Report("运行组件已就绪");
                return;
            }

            status?.Report($"正在释放内置组件 ({missing.Count})...");
            Directory.CreateDirectory(ToolsDirectory);

            var index = 0;
            foreach (var tool in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
                status?.Report($"正在释放 {tool.FileName} ({index}/{missing.Count})...");
                ExtractTool(assembly, tool);
            }

            var stillMissing = Tools
                .Where(tool => !IsValidFile(GetLocalPath(tool.FileName)))
                .Select(tool => tool.FileName)
                .ToList();

            if (stillMissing.Count > 0)
            {
                throw new InvalidOperationException(
                    "内置组件释放失败: " + string.Join("、", stillMissing));
            }

            status?.Report("运行组件已就绪");
        }

        private void ExtractTool(Assembly assembly, EmbeddedTool tool)
        {
            var targetPath = GetLocalPath(tool.FileName);
            var tempPath = targetPath + ".extract";

            using (var stream = assembly.GetManifestResourceStream(tool.ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"未找到内置资源: {tool.FileName}");
                }

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                using (var output = File.Create(tempPath))
                {
                    stream.CopyTo(output);
                }
            }

            if (!IsValidFile(tempPath))
            {
                throw new InvalidOperationException($"内置资源无效: {tool.FileName}");
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }

        private string GetLocalPath(string fileName)
        {
            return Path.Combine(ToolsDirectory, fileName);
        }

        private static bool IsValidFile(string path)
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private sealed class EmbeddedTool
        {
            public EmbeddedTool(string fileName, string resourceName)
            {
                FileName = fileName;
                ResourceName = resourceName;
            }

            public string FileName { get; }
            public string ResourceName { get; }
        }
    }
}
