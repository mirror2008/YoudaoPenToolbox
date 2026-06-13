using System;
using System.Threading.Tasks;
using System.Windows;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Models;
using YoudaoPenToolbox.Services;

namespace YoudaoPenToolbox.Views
{
    public partial class RemoteFileViewerWindow : Window
    {
        private RemoteFileContent _pendingContent;
        private bool _isLoading;

        public RemoteFileViewerWindow(RemoteFileItem file, RemoteFileAction viewMode)
        {
            InitializeComponent();
            File = file;
            ViewMode = viewMode;

            var fileName = DeviceFileBrowserService.GetEntryFileName(file);
            Title = viewMode == RemoteFileAction.OpenBinary
                ? $"二进制查看 - {fileName}"
                : $"记事本 - {fileName}";

            TitleText.Text = fileName;
            InfoText.Text = $"路径: {file.FullPath}  |  正在准备预览...";

            Loaded += RemoteFileViewerWindow_Loaded;
        }

        public RemoteFileItem File { get; }
        public RemoteFileAction ViewMode { get; }
        public RemoteFileContent ContentInfo { get; private set; }

        public void LoadContent(RemoteFileContent content)
        {
            _pendingContent = content;
            if (IsLoaded && !_isLoading)
            {
                _ = InitializeContentAsync();
            }
        }

        private async void RemoteFileViewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_pendingContent != null && !_isLoading)
            {
                await InitializeContentAsync().ConfigureAwait(true);
            }
        }

        private async Task InitializeContentAsync()
        {
            if (_pendingContent == null || _isLoading)
            {
                return;
            }

            _isLoading = true;
            LoadingPanel.Visibility = Visibility.Visible;
            BinaryViewBorder.Visibility = Visibility.Collapsed;
            TextViewBorder.Visibility = Visibility.Collapsed;

            var content = _pendingContent;
            try
            {
                if (ViewMode == RemoteFileAction.OpenBinary)
                {
                    LoadingText.Text = "正在准备二进制视图（虚拟化渲染，按需生成）...";
                    var lines = await Task.Run(() => new HexDumpVirtualCollection(content.Data)).ConfigureAwait(true);
                    BinaryListBox.ItemsSource = lines;
                    BinaryViewBorder.Visibility = Visibility.Visible;
                    ContentInfo = content;
                    InfoText.Text = BuildInfoText(File, content, lines.Count);
                }
                else
                {
                    LoadingText.Text = "正在后台解码文本并建立行索引...";
                    var lines = await LazyTextLineCollection.CreateAsync(content.Data).ConfigureAwait(true);
                    TextListBox.ItemsSource = lines;
                    TextViewBorder.Visibility = Visibility.Visible;
                    ContentInfo = content;
                    InfoText.Text = BuildInfoText(File, content, lines.Count);
                }
            }
            catch (Exception ex)
            {
                InfoText.Text = $"加载失败: {ex.Message}";
                System.Windows.MessageBox.Show(
                    ex.Message,
                    "打开文件失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                _isLoading = false;
            }
        }

        private static string BuildInfoText(RemoteFileItem file, RemoteFileContent content, int lineCount)
        {
            var truncated = content.IsTruncated ? "（仅显示前 2 MB 预览）" : string.Empty;
            return $"路径: {file.FullPath}  |  预览: {FormatBytes(content.Data.Length)} / {FormatBytes(content.TotalSizeBytes)}  |  行数: {lineCount:N0}{truncated}";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }
    }
}
