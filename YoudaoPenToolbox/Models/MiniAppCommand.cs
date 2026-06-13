using System.Linq;

namespace YoudaoPenToolbox.Models
{
    public class MiniAppCommand
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public string Notes { get; set; }
        public string CommandTemplate { get; set; }
        public MiniAppParameter[] ParameterDetails { get; set; }

        public string[] Parameters => ParameterDetails?.Select(p => p.Name).ToArray();
        public bool RequiresParameters => ParameterDetails != null && ParameterDetails.Length > 0;
        public string DisplayText => $"[{Category}] {Description}";

        public string BuildPreview(params string[] args)
        {
            if (!RequiresParameters)
            {
                return CommandTemplate;
            }

            if (args == null || args.Length == 0)
            {
                var placeholders = ParameterDetails.Select(p => p.Example ?? p.Name).ToArray();
                try
                {
                    return string.Format(CommandTemplate, placeholders.Cast<object>().ToArray());
                }
                catch
                {
                    return CommandTemplate;
                }
            }

            return string.Format(CommandTemplate, args.Cast<object>().ToArray());
        }

        public string BuildHelpText()
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"命令: {Name}",
                $"说明: {Description}",
                $"用法: {Usage}"
            };

            if (ParameterDetails != null)
            {
                foreach (var p in ParameterDetails)
                {
                    lines.Add($"  · {p.Label} ({p.Name}): {p.Hint}");
                    if (!string.IsNullOrWhiteSpace(p.Example))
                    {
                        lines.Add($"    示例: {p.Example}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(Notes))
            {
                lines.Add($"提示: {Notes}");
            }

            return string.Join("\r\n", lines);
        }
    }
}
