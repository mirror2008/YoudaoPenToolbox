namespace YoudaoPenToolbox.Models
{
    public class AdbPersistStatus
    {
        public bool ShellAccessible { get; set; }
        public bool AuthFileExists { get; set; }
        public bool SkipReHookInstalled { get; set; }
        public bool SkipReScriptExists { get; set; }
        public string SkipReScriptHead { get; set; }
        public string Summary { get; set; }

        public bool IsPersistEnabled => SkipReHookInstalled;
    }

    public enum AdbPersistEnsureAction
    {
        SkippedShellLocked,
        AlreadyEnabled,
        Configured,
        Failed
    }

    public sealed class AdbPersistEnsureResult
    {
        public AdbPersistEnsureAction Action { get; set; }
        public AdbPersistStatus Status { get; set; }
        public string Log { get; set; }
        public string ErrorMessage { get; set; }
    }
}
