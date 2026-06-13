using System;
using System.IO;
using System.Threading.Tasks;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public class RemoteFileContent
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public long TotalSizeBytes { get; set; }
        public bool IsTruncated { get; set; }
    }

    public partial class DeviceFileBrowserService
    {
        public const long DefaultPreviewLimitBytes = 2 * 1024 * 1024;

        public Task<RemoteFileContent> ReadFileBytesAsync(string serial, RemoteFileItem item, long maxBytes = DefaultPreviewLimitBytes)
        {
            return Task.Run(async () => await ReadFileBytesCoreAsync(serial, item, maxBytes).ConfigureAwait(false));
        }

        private async Task<RemoteFileContent> ReadFileBytesCoreAsync(string serial, RemoteFileItem item, long maxBytes)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var tempFile = Path.Combine(Path.GetTempPath(), $"ypt_file_{Guid.NewGuid():N}.bin");
            try
            {
                var pulled = await _adbService.PullFileAsync(serial, item.FullPath, tempFile).ConfigureAwait(false);
                if (!File.Exists(tempFile) && !pulled)
                {
                    throw new InvalidOperationException("无法从设备读取文件");
                }

                var fileInfo = new FileInfo(tempFile);
                var totalSize = item.SizeBytes > 0 ? item.SizeBytes : fileInfo.Length;
                var isTruncated = fileInfo.Length > maxBytes;
                var readLength = isTruncated ? maxBytes : fileInfo.Length;

                byte[] data;
                using (var stream = File.OpenRead(tempFile))
                using (var memory = new MemoryStream((int)readLength))
                {
                    await CopyLimitedAsync(stream, memory, readLength).ConfigureAwait(false);
                    data = memory.ToArray();
                }

                return new RemoteFileContent
                {
                    Data = data,
                    TotalSizeBytes = totalSize,
                    IsTruncated = isTruncated
                };
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {

                }
            }
        }

        private static async Task CopyLimitedAsync(Stream source, Stream destination, long maxBytes)
        {
            var buffer = new byte[64 * 1024];
            long totalRead = 0;
            int read;
            while (totalRead < maxBytes
                   && (read = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, maxBytes - totalRead)).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                totalRead += read;
            }
        }

        public static string GetEntryFileName(RemoteFileItem item)
        {
            if (item == null)
            {
                return "remote_file";
            }

            var rawName = item.Name ?? string.Empty;
            var arrowIndex = rawName.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIndex >= 0)
            {
                rawName = rawName.Substring(0, arrowIndex).Trim();
            }

            return string.IsNullOrWhiteSpace(rawName) ? "remote_file" : rawName;
        }
    }
}
