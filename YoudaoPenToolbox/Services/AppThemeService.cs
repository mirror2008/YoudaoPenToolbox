using System;
using System.Windows;
using System.Windows.Threading;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Services
{
    public sealed class AppThemeService
    {
        private const string SkinDefaultUri = "pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml";
        private const string SkinDarkUri = "pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml";
        private const string ThemeUri = "pack://application:,,,/HandyControl;component/Themes/Theme.xaml";

        private static readonly AppThemeService InstanceValue = new AppThemeService();

        private readonly DispatcherTimer _autoTimer;
        private readonly string _settingsPath;
        private bool _initialized;
        private bool _isDarkMode;

        private AppThemeService()
        {
            _settingsPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "YoudaoPenToolbox",
                "theme.txt");

            _autoTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _autoTimer.Tick += (_, __) => ApplyCurrentTheme(force: false);
        }

        public static AppThemeService Instance => InstanceValue;

        public AppThemePreference Preference { get; private set; } = AppThemePreference.Auto;

        public bool IsDarkMode => _isDarkMode;

        public event EventHandler ThemeChanged;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            LoadPreference();
            ApplyCurrentTheme(force: true);
            UpdateAutoTimer();
            Application.Current.Exit += (_, __) => _autoTimer.Stop();
            _initialized = true;
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetPreference(AppThemePreference preference)
        {
            Preference = preference;
            SavePreference();
            ApplyCurrentTheme(force: true);
            UpdateAutoTimer();
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 18:00–05:59 为夜间（深色），06:00–17:59 为白天（浅色）。
        /// </summary>
        public static bool IsNightTime(DateTime time)
        {
            var hour = time.Hour;
            return hour >= 18 || hour < 6;
        }

        private void LoadPreference()
        {
            try
            {
                if (!System.IO.File.Exists(_settingsPath))
                {
                    return;
                }

                var text = System.IO.File.ReadAllText(_settingsPath).Trim();
                if (Enum.TryParse(text, true, out AppThemePreference preference))
                {
                    Preference = preference;
                }
            }
            catch
            {
            }
        }

        private void SavePreference()
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                System.IO.File.WriteAllText(_settingsPath, Preference.ToString());
            }
            catch
            {
            }
        }

        private void UpdateAutoTimer()
        {
            if (Preference == AppThemePreference.Auto)
            {
                if (!_autoTimer.IsEnabled)
                {
                    _autoTimer.Start();
                }

                return;
            }

            _autoTimer.Stop();
        }

        private bool ResolveIsDark()
        {
            switch (Preference)
            {
                case AppThemePreference.Light:
                    return false;
                case AppThemePreference.Dark:
                    return true;
                default:
                    return IsNightTime(DateTime.Now);
            }
        }

        private void ApplyCurrentTheme(bool force)
        {
            var isDark = ResolveIsDark();
            if (!force && isDark == _isDarkMode)
            {
                return;
            }

            _isDarkMode = isDark;
            ApplySkin(isDark);
        }

        private static void ApplySkin(bool isDark)
        {
            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            var skinUri = new Uri(isDark ? SkinDarkUri : SkinDefaultUri, UriKind.Absolute);
            var themeUri = new Uri(ThemeUri, UriKind.Absolute);

            var skinHost = new ResourceDictionary();
            skinHost.MergedDictionaries.Add(new ResourceDictionary { Source = skinUri });
            skinHost.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            var merged = app.Resources.MergedDictionaries;
            if (merged.Count == 0)
            {
                merged.Add(skinHost);
            }
            else
            {
                merged[0] = skinHost;
            }

            RefreshAllWindows(app);
        }

        private static void RefreshAllWindows(Application app)
        {
            foreach (Window window in app.Windows)
            {
                if (window == null)
                {
                    continue;
                }

                window.Background = app.FindResource("RegionBrush") as System.Windows.Media.Brush;
                ThemeResourceHelper.ReloadAllDataGrids(window);
                ThemeResourceHelper.RefreshElement(window);
                window.UpdateLayout();
            }
        }
    }
}
