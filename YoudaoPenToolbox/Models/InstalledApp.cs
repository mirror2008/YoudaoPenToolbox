namespace YoudaoPenToolbox.Models
{
    public class InstalledApp
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Category { get; set; }
        public bool CanUninstall { get; set; }
        public bool IsThirdParty { get; set; }
        public bool IsProtectedSystemApp { get; set; }
        public string InstallPath { get; set; }
        public string PackageDir { get; set; }
        public long SizeKb { get; set; }

        public string AppType => IsThirdParty ? "第三方" : (IsProtectedSystemApp ? "系统·受保护" : "系统");
        public string UninstallDisplay => IsProtectedSystemApp ? "受保护" : "是";
        public string SizeDisplay => SizeKb > 0 ? FormatSize(SizeKb) : "计算中...";
        public string DisplayText => $"{Name}  v{Version}  [{SizeDisplay}]";

        private static string FormatSize(long kb)
        {
            if (kb >= 1024 * 1024)
            {
                return $"{kb / 1024.0 / 1024.0:F2} GB";
            }

            if (kb >= 1024)
            {
                return $"{kb / 1024.0:F1} MB";
            }

            return $"{kb} KB";
        }
    }
}
