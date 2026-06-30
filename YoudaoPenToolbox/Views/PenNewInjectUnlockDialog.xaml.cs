using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using YoudaoPenToolbox.Helpers;
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
            DialogAnimationHelper.Register(this, () => _allowClose);
        }

        private void PayUnlockButton_Click(object sender, RoutedEventArgs e)
        {
            DialogAnimationHelper.TransitionPanels(IntroPanel, PaymentPanel);
        }

        private void ExternalLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            DialogAnimationHelper.TransitionPanels(PaymentPanel, IntroPanel);
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
            if (PaymentPanel.Visibility == Visibility.Visible)
            {
                DialogAnimationHelper.TransitionPanels(PaymentPanel, ProgressPanel);
            }
            else if (IntroPanel.Visibility == Visibility.Visible)
            {
                DialogAnimationHelper.TransitionPanels(IntroPanel, ProgressPanel);
            }
            else
            {
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressPanel.Opacity = 1;
            }

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
