using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Services;

namespace YoudaoPenToolbox.Views
{
    public partial class UpdateWindow : Window
    {
        private readonly AppUpdateCheckResult _checkResult;
        private readonly AppUpdateService _updateService;
        private bool _allowClose;
        private CancellationTokenSource _downloadCts;

        public UpdateWindow(AppUpdateCheckResult checkResult, AppUpdateService updateService)
        {
            InitializeComponent();
            DialogAnimationHelper.Register(this, () => _allowClose);
            _checkResult = checkResult ?? throw new ArgumentNullException(nameof(checkResult));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));

            VersionTextBlock.Text =
                $"当前版本 v{_updateService.GetCurrentVersionText()}  →  最新版本 v{_checkResult.RemoteVersionText}";
            Loaded += UpdateWindow_Loaded;
        }

        private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RunUpdateAsync().ConfigureAwait(true);
        }

        private async Task RunUpdateAsync()
        {
            _allowClose = false;
            ActionPanel.Visibility = Visibility.Collapsed;
            DownloadProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = string.Empty;
            StatusTextBlock.Text = "正在下载更新...";

            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<DownloadProgress>(UpdateProgress);
                var localPath = await _updateService.DownloadUpdateAsync(
                    _checkResult.DownloadUrl,
                    progress,
                    _downloadCts.Token).ConfigureAwait(true);

                StatusTextBlock.Text = "下载完成，正在安装并重启...";
                DownloadProgressBar.IsIndeterminate = true;
                ProgressTextBlock.Text = string.Empty;

                _allowClose = true;
                _updateService.ApplyUpdateAndRestart(localPath);
            }
            catch (Exception ex)
            {
                DownloadProgressBar.IsIndeterminate = false;
                StatusTextBlock.Text = $"更新失败: {ex.Message}";
                ProgressTextBlock.Text = string.Empty;
                ActionPanel.Visibility = Visibility.Visible;
            }
        }

        private void UpdateProgress(DownloadProgress progress)
        {
            if (progress?.Percent is double percent)
            {
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = percent;
                ProgressTextBlock.Text = $"{percent:F0}%";
                return;
            }

            DownloadProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = FormatBytes(progress?.BytesReceived ?? 0);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            await RunUpdateAsync().ConfigureAwait(true);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _allowClose = true;
            DialogResult = false;
            Close();
        }
    }
}
