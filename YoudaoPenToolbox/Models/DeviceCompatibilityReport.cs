using System.Text;

namespace YoudaoPenToolbox.Models
{
    public class DeviceCompatibilityReport
    {
        public string UnameMachine { get; set; }
        public string DeviceTreeCompatible { get; set; }
        public string UsrBinListing { get; set; }
        public string DevListing { get; set; }
        public string GstLaunchOutput { get; set; }
        public string MiniappCliOutput { get; set; }

        public bool HasGstLaunchInUsrBin =>
            ContainsBinaryName(UsrBinListing, "gst-launch-1.0")
            || ContainsBinaryName(UsrBinListing, "gst-launch");

        public bool HasFfmpegInUsrBin => ContainsBinaryName(UsrBinListing, "ffmpeg");

        public bool GstLaunchAvailable =>
            HasGstLaunchInUsrBin
            || LooksLikeAvailableCommand(GstLaunchOutput);

        public bool MiniappCliAvailable => LooksLikeAvailableCommand(MiniappCliOutput);

        public string FormatForDisplay()
        {
            var builder = new StringBuilder();
            AppendSection(builder, "uname -m", UnameMachine);
            AppendSection(builder, "cat /proc/device-tree/compatible", DeviceTreeCompatible);
            AppendSection(builder, "ls -la /usr/bin/", UsrBinListing);
            AppendSection(builder, "ls /dev", DevListing);
            AppendSection(builder, "gst-launch-1.0", GstLaunchOutput);
            AppendSection(builder, "miniapp_cli", MiniappCliOutput);
            return builder.ToString().TrimEnd();
        }

        private static void AppendSection(StringBuilder builder, string command, string output)
        {
            builder.AppendLine($"=== {command} ===");
            builder.AppendLine(string.IsNullOrWhiteSpace(output) ? "(无输出)" : output.TrimEnd());
            builder.AppendLine();
        }

        private static bool ContainsBinaryName(string listing, string binaryName)
        {
            if (string.IsNullOrWhiteSpace(listing) || string.IsNullOrWhiteSpace(binaryName))
            {
                return false;
            }

            var normalized = listing.ToLowerInvariant();
            var token = binaryName.ToLowerInvariant();
            return normalized.IndexOf(" " + token, System.StringComparison.Ordinal) >= 0
                   || normalized.IndexOf("/" + token, System.StringComparison.Ordinal) >= 0
                   || normalized.IndexOf(">" + token, System.StringComparison.Ordinal) >= 0;
        }

        private static bool LooksLikeAvailableCommand(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            var text = output.ToLowerInvariant();
            if (text.IndexOf("not found", System.StringComparison.Ordinal) >= 0
                || text.IndexOf("no such file", System.StringComparison.Ordinal) >= 0
                || text.IndexOf("cannot execute", System.StringComparison.Ordinal) >= 0
                || text.IndexOf("permission denied", System.StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return true;
        }
    }
}
