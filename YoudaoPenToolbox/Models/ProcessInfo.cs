namespace YoudaoPenToolbox.Models
{
    public class ProcessInfo
    {
        public int Pid { get; set; }
        public int Ppid { get; set; }
        public string User { get; set; }
        public string Stat { get; set; }
        public string VirtualMemory { get; set; }
        public double MemoryPercent { get; set; }
        public int CpuCore { get; set; }
        public double CpuPercent { get; set; }
        public string Command { get; set; }
        public string ExecutablePath { get; set; }

        public string MemoryPercentDisplay => $"{MemoryPercent:F1}%";
        public string CpuPercentDisplay => $"{CpuPercent:F1}%";
        public string PathDisplay => string.IsNullOrWhiteSpace(ExecutablePath) ? Command : ExecutablePath;

        public string ShortName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ExecutablePath))
                {
                    var fileName = System.IO.Path.GetFileName(ExecutablePath);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        return fileName;
                    }
                }

                if (string.IsNullOrWhiteSpace(Command))
                {
                    return $"PID {Pid}";
                }

                var command = Command;
                if (command.StartsWith("{", System.StringComparison.Ordinal))
                {
                    var closeBrace = command.IndexOf('}');
                    if (closeBrace >= 0 && closeBrace + 1 < command.Length)
                    {
                        command = command.Substring(closeBrace + 1).Trim();
                    }
                }

                var firstToken = command.Split(' ')[0];
                return System.IO.Path.GetFileName(firstToken) ?? firstToken;
            }
        }
    }
}
