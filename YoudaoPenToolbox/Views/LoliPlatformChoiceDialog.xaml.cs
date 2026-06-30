using System.Windows;
using YoudaoPenToolbox.Helpers;

namespace YoudaoPenToolbox.Views
{
    public enum LoliPlatformChoice
    {
        None,
        Rockchip,
        Cvi
    }

    public partial class LoliPlatformChoiceDialog : Window
    {
        public LoliPlatformChoiceDialog()
        {
            InitializeComponent();
            DialogAnimationHelper.Register(this);
        }

        public LoliPlatformChoice Choice { get; private set; } = LoliPlatformChoice.None;

        private void Rk_Click(object sender, RoutedEventArgs e)
        {
            Choice = LoliPlatformChoice.Rockchip;
            DialogResult = true;
            Close();
        }

        private void Cvi_Click(object sender, RoutedEventArgs e)
        {
            Choice = LoliPlatformChoice.Cvi;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = LoliPlatformChoice.None;
            DialogResult = false;
            Close();
        }
    }
}
