using System.Windows;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Views
{
    public partial class RemoteFileActionDialog : Window
    {
        public RemoteFileActionDialog(RemoteFileItem file)
        {
            InitializeComponent();
            File = file;
            FileNameText.Text = file.Name;
            FilePathText.Text = file.FullPath;
            FileSizeText.Text = $"大小: {file.SizeDisplay}";
        }

        public RemoteFileItem File { get; }
        public RemoteFileAction? SelectedAction { get; private set; }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = RemoteFileAction.Download;
            DialogResult = true;
            Close();
        }

        private void OpenText_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = RemoteFileAction.OpenText;
            DialogResult = true;
            Close();
        }

        private void OpenBinary_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = RemoteFileAction.OpenBinary;
            DialogResult = true;
            Close();
        }
    }
}
