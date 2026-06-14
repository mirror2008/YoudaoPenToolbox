using System.Windows;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Views
{
    public partial class InstallConfirmDialog : Window
    {
        public InstallConfirmDialog(AmrPackageInfo package, string deviceName)
        {
            InitializeComponent();
            DialogAnimationHelper.Register(this);
            Package = package;
            DeviceName = deviceName;

            AppNameText.Text = package.AppName;
            VersionText.Text = package.Version;
            AppIdText.Text = package.AppId;
            SizeText.Text = package.SizeDisplay;
            DeviceNameText.Text = deviceName;
            FileNameText.Text = package.FileName;

            if (package.Icon != null)
            {
                IconImage.Source = package.Icon;
                IconPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                IconImage.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrWhiteSpace(package.ErrorMessage))
            {
                WarningText.Text = package.ErrorMessage;
                WarningText.Visibility = Visibility.Visible;
            }
            else
            {
                WarningText.Visibility = Visibility.Collapsed;
            }
        }

        public AmrPackageInfo Package { get; }
        public string DeviceName { get; }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
