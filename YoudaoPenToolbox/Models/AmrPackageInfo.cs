using System;
using System.IO;
using System.Windows.Media;

namespace YoudaoPenToolbox.Models
{
    public class AmrPackageInfo : IDisposable
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSizeBytes { get; set; }
        public string AppName { get; set; }
        public string Version { get; set; }
        public string AppId { get; set; }
        public ImageSource Icon { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }

        public string SizeDisplay => FormatSize(FileSizeBytes);

        public string SummaryLine => IsValid
            ? $"{AppName}  v{Version}  ·  {SizeDisplay}"
            : FileName;

        private string _tempIconPath;
        private string _tempFolder;

        public static string FormatSize(long bytes)
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

        internal void SetTempPaths(string folder, string iconPath)
        {
            _tempFolder = folder;
            _tempIconPath = iconPath;
        }

        public void Dispose()
        {
            Icon = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(_tempIconPath) && File.Exists(_tempIconPath))
                {
                    File.Delete(_tempIconPath);
                }

                if (!string.IsNullOrWhiteSpace(_tempFolder) && Directory.Exists(_tempFolder))
                {
                    Directory.Delete(_tempFolder, true);
                }
            }
            catch
            {

            }
        }
    }
}
