namespace YoudaoPenToolbox.Models
{
    public class LoliPlatformInfo
    {
        public string PlatformType { get; set; }
        public string PlatformLabel { get; set; }
        public string RepositorySubPath { get; set; }
        public string DetectionDetail { get; set; }
        public string CompatibilityReport { get; set; }
        public DeviceCompatibilityReport Probe { get; set; }
        public bool IsSupported => !string.IsNullOrWhiteSpace(RepositorySubPath);
    }
}
