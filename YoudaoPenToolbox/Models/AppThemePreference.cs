namespace YoudaoPenToolbox.Models
{
    public enum AppThemePreference
    {
        Auto,
        Light,
        Dark
    }

    public sealed class ThemePreferenceOption
    {
        public ThemePreferenceOption(AppThemePreference value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public AppThemePreference Value { get; }

        public string DisplayName { get; }
    }
}
