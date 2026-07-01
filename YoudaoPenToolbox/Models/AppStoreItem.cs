using System;
using System.Windows.Media;
using YoudaoPenToolbox.Helpers;

namespace YoudaoPenToolbox.Models
{
    public class AppStoreItem : ViewModelBase
    {
        private ImageSource _icon;
        private bool _isInstalled;
        private bool _isIconLoading;

        public string Id { get; set; }
        public string Name { get; set; }
        public string AppId { get; set; }
        public string Version { get; set; }
        public long SizeBytes { get; set; }
        public string SizeDisplay { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedAtDisplay { get; set; }
        public string Uploader { get; set; }
        public int UploaderId { get; set; }
        public string UploaderDisplay
        {
            get
            {
                if (UploaderId > 0 && !string.IsNullOrWhiteSpace(Uploader) && Uploader != "-")
                {
                    return Uploader.Trim() + " (#" + UploaderId + ")";
                }

                if (UploaderId > 0)
                {
                    return "#" + UploaderId;
                }

                return string.IsNullOrWhiteSpace(Uploader) ? "-" : Uploader;
            }
        }
        public string Platform { get; set; }
        public string PlatformDisplay { get; set; }
        public string PenModel { get; set; }
        public string Description { get; set; }
        public bool IsRk => string.Equals(Platform, "rk", StringComparison.OrdinalIgnoreCase);
        public bool IsCvi => string.Equals(Platform, "cvi", StringComparison.OrdinalIgnoreCase);

        public string FileName { get; set; }
        public string IconUrl { get; set; }
        public string DownloadUrl { get; set; }

        public string DescriptionShort
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Description))
                {
                    return "-";
                }

                var text = Description.Trim().Replace("\r\n", " ").Replace('\n', ' ');
                return text.Length <= 20 ? text : text.Substring(0, 20) + "…";
            }
        }

        public string DescriptionDisplay =>
            string.IsNullOrWhiteSpace(Description) ? "暂无简介" : Description.Trim();

        public ImageSource Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set => SetProperty(ref _isInstalled, value);
        }

        public bool IsIconLoading
        {
            get => _isIconLoading;
            set => SetProperty(ref _isIconLoading, value);
        }

        public string SummaryLine =>
            $"{Name}  v{Version}  ·  {SizeDisplay}  ·  {Uploader}  ·  {UploadedAtDisplay}";
    }
}
