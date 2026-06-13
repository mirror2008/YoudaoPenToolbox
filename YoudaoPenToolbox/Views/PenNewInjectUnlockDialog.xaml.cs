using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YoudaoPenToolbox.Services;

namespace YoudaoPenToolbox.Views
{
    public partial class PenNewInjectUnlockDialog : Window
    {
        private readonly PenNewInjectService _service = new PenNewInjectService();
        private CancellationTokenSource _downloadCts;
        private bool _allowClose = true;

        public PenNewInjectUnlockDialog()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }

            base.OnClosing(e);
        }

        private void PayUnlockButton_Click(object sender, RoutedEventArgs e)
        {
            IntroPanel.Visibility = Visibility.Collapsed;
            PaymentPanel.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            PaymentPanel.Visibility = Visibility.Collapsed;
            IntroPanel.Visibility = Visibility.Visible;
        }

        private async void KeyReadyButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync().ConfigureAwait(true);
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadAsync().ConfigureAwait(true);
        }

        private async Task StartDownloadAsync()
        {
            PaymentPanel.Visibility = Visibility.Collapsed;
            IntroPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            RetryPanel.Visibility = Visibility.Collapsed;
            DownloadProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = string.Empty;

            _allowClose = false;
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();

            try
            {
                var status = new Progress<string>(text => StatusTextBlock.Text = text);
                var progress = new Progress<DownloadProgress>(UpdateProgress);
                var exePath = await _service.DownloadExtractAndGetExecutableAsync(
                    status,
                    progress,
                    _downloadCts.Token).ConfigureAwait(true);

                StatusTextBlock.Text = "正在启动 PenNewInject...";
                _service.Launch(exePath);

                _allowClose = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                DownloadProgressBar.IsIndeterminate = false;
                StatusTextBlock.Text = $"操作失败: {ex.Message}";
                ProgressTextBlock.Text = string.Empty;
                RetryPanel.Visibility = Visibility.Visible;
                _allowClose = true;
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
    }
}
