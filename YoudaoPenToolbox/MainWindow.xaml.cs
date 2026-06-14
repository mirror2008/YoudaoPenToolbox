using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Models;
using YoudaoPenToolbox.Services;
using YoudaoPenToolbox.ViewModels;

namespace YoudaoPenToolbox
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly bool _skipEntranceAnimation;
        private bool _applicationInitialized;

        public MainWindow(bool skipEntranceAnimation = false)
        {
            _skipEntranceAnimation = skipEntranceAnimation;
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            AppThemeService.Instance.ThemeChanged += OnAppThemeChanged;
        }

        public async Task InitializeApplicationAsync()
        {
            if (_applicationInitialized)
            {
                return;
            }

            _applicationInitialized = true;
            UpdateThemeButtonStates();
            ThemeResourceHelper.ReloadAllDataGrids(this);
            await _viewModel.InitializeAsync().ConfigureAwait(true);
            ThemeResourceHelper.ReloadAllDataGrids(this);
        }

        private void OnAppThemeChanged(object sender, System.EventArgs e)
        {
            Background = FindResource("RegionBrush") as Brush;
            UpdateThemeButtonStates();
            ThemeResourceHelper.ReloadAllDataGrids(this);
            ThemeResourceHelper.RefreshElement(this);
        }

        private async void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabControl || MainTabControl.SelectedItem is not TabItem tab)
            {
                return;
            }

            UiAnimationHelper.PlayTabContentIn(tab);

            if (tab.Header?.ToString() == "刷机")
            {
                await _viewModel.EnsurePartitionsLoadedAsync();
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowLayoutHelper.ApplyMainWindowBounds(this);

            if (!_skipEntranceAnimation)
            {
                UiAnimationHelper.PlayWindowEntrance(RootContent);
            }

            if (!_applicationInitialized)
            {
                await InitializeApplicationAsync().ConfigureAwait(true);
            }
        }

        private void ThemeAutoButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SetThemePreference(AppThemePreference.Auto);
        }

        private void ThemeLightButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SetThemePreference(AppThemePreference.Light);
        }

        private void ThemeDarkButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SetThemePreference(AppThemePreference.Dark);
        }

        private void UpdateThemeButtonStates()
        {
            var preference = AppThemeService.Instance.Preference;
            var primary = (Brush)FindResource("PrimaryBrush");
            var secondary = (Brush)FindResource("SecondaryTextBrush");

            ApplyThemeButtonState(ThemeAutoButton, preference == AppThemePreference.Auto, primary, secondary);
            ApplyThemeButtonState(ThemeLightButton, preference == AppThemePreference.Light, primary, secondary);
            ApplyThemeButtonState(ThemeDarkButton, preference == AppThemePreference.Dark, primary, secondary);
        }

        private static void ApplyThemeButtonState(
            Button button,
            bool isActive,
            Brush primary,
            Brush inactiveText)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isActive ? primary : Brushes.Transparent;
            button.Foreground = isActive ? Brushes.White : inactiveText;
            button.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            UiAnimationHelper.AnimateThemeSegment(button, isActive);
        }

        private void MainWindow_Closed(object sender, System.EventArgs e)
        {
            _viewModel.Cleanup();
        }

        private void SearchApps_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyAppFilter();
        }

        private void AboutLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            DropZone_DragOver(sender, e);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(f => f.EndsWith(".amr", System.StringComparison.OrdinalIgnoreCase)))
                {
                    e.Effects = DragDropEffects.Copy;
                    DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    return;
                }
            }

            DropZone.BorderBrush = GetPrimaryBorderBrush();
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ResetDropZoneVisual();
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ResetDropZoneVisual();

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var amr = files?.FirstOrDefault(f => f.EndsWith(".amr", System.StringComparison.OrdinalIgnoreCase));
            if (amr == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new System.Action(() =>
            {
                _ = _viewModel.InstallAmrAsync(amr);
            }));
        }

        private void DropZone_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = true;
            e.Handled = true;
        }

        private void ResetDropZoneVisual()
        {
            DropZone.BorderBrush = GetPrimaryBorderBrush();
            if (Mouse.Captured != null)
            {
                Mouse.Capture(null);
            }
        }

        private void FileManagerDropZone_DragEnter(object sender, DragEventArgs e)
        {
            FileManagerDropZone_DragOver(sender, e);
        }

        private void FileManagerDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (_viewModel.SelectedDevice == null)
            {
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Any(System.IO.File.Exists))
                {
                    e.Effects = DragDropEffects.Copy;
                    FileManagerDropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    return;
                }
            }

            FileManagerDropZone.BorderBrush = GetPrimaryBorderBrush();
        }

        private void FileManagerDropZone_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ResetFileManagerDropZoneVisual();
        }

        private void FileManagerDropZone_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ResetFileManagerDropZoneVisual();

            if (_viewModel.SelectedDevice == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
                ?.Where(System.IO.File.Exists)
                .ToArray();
            if (files == null || files.Length == 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new System.Action(() =>
            {
                _ = _viewModel.UploadRemoteFilesAsync(files);
            }));
        }

        private void FileManagerDropZone_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            e.UseDefaultCursors = true;
            e.Handled = true;
        }

        private void ResetFileManagerDropZoneVisual()
        {
            FileManagerDropZone.BorderBrush = GetPrimaryBorderBrush();
            if (Mouse.Captured != null)
            {
                Mouse.Capture(null);
            }
        }

        private void RemoteFilesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new System.Action(() =>
            {
                _ = _viewModel.HandleRemoteFileDoubleClickAsync();
            }));
        }

        private void RemoteFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItems = RemoteFilesDataGrid.SelectedItems
                .Cast<object>()
                .OfType<Models.RemoteFileItem>()
                .ToList();
            _viewModel.SetRemoteFileSelection(selectedItems);
        }

        private void RemoteFilesDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                RemoteFilesDataGrid.SelectAll();
                e.Handled = true;
            }
        }

        private async void RemotePathInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _viewModel.NavigateRemotePathCommand == null)
            {
                return;
            }

            if (!_viewModel.NavigateRemotePathCommand.CanExecute(null))
            {
                return;
            }

            e.Handled = true;
            _viewModel.NavigateRemotePathCommand.Execute(null);
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private Brush GetPrimaryBorderBrush()
        {
            return (Brush)FindResource("BorderBrush");
        }

        private async void QuickMemoryUsage_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuick("memoryUsage");
        }

        private async void QuickMemoryUsageGc_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuick("memoryUsageGC");
        }

        private async void DumpMemory_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedDevice == null)
            {
                return;
            }

            var confirm = AppMessageBox.Show(
                "将导出 QuickJS 内存快照到设备 /tmp/httpdump.snapshot，是否继续？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            await ExecuteQuick("dumpMemory");
        }

        private async void CaptureFb_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedDevice == null)
            {
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 图片|*.png",
                FileName = $"capturefb_{System.DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            var remotePath = $"/tmp/capturefb_{System.DateTime.Now.Ticks}.png";
            var cli = new Services.MiniAppCliService(new Services.AdbService { AdbPath = _viewModel.AdbPath });
            var result = await cli.ExecuteRawAsync(_viewModel.SelectedDevice.Serial, $"captureFB {remotePath}");
            var adb = new Services.AdbService { AdbPath = _viewModel.AdbPath };
            await adb.PullFileAsync(_viewModel.SelectedDevice.Serial, remotePath, dlg.FileName);
            _viewModel.CommandOutput = $"[{System.DateTime.Now:HH:mm:ss}] captureFB\r\n{result}\r\n保存至: {dlg.FileName}\r\n\r\n{_viewModel.CommandOutput}";
        }

        private async System.Threading.Tasks.Task ExecuteQuick(string command)
        {
            if (_viewModel.SelectedDevice == null)
            {
                return;
            }

            var cli = new Services.MiniAppCliService(new Services.AdbService { AdbPath = _viewModel.AdbPath });
            var result = await cli.ExecuteRawAsync(_viewModel.SelectedDevice.Serial, command);
            _viewModel.CommandOutput = $"[{System.DateTime.Now:HH:mm:ss}] {command}\r\n{result}\r\n\r\n{_viewModel.CommandOutput}";
        }

        private async void AdbTerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || _viewModel.ExecuteAdbCommandCommand == null)
            {
                return;
            }

            if (!_viewModel.ExecuteAdbCommandCommand.CanExecute(null))
            {
                return;
            }

            e.Handled = true;
            _viewModel.ExecuteAdbCommandCommand.Execute(null);
            await System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
