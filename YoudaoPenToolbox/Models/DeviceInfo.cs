namespace YoudaoPenToolbox.Models
{
    public class DeviceInfo
    {
        public string Serial { get; set; }
        public string State { get; set; }
        public string Model { get; set; }
        public string Brand { get; set; }
        public string Manufacturer { get; set; }
        public string AndroidVersion { get; set; }
        public string ProductName { get; set; }
        public string Hostname { get; set; }
        public string Platform { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";
        public string DetailInfo => $"{Brand} · {AndroidVersion} · {Platform}";
    }
}
