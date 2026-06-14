using System.Windows;
using System.Windows.Input;
using YoudaoPenToolbox.Helpers;

namespace YoudaoPenToolbox.Views
{
    public partial class TextInputDialog : Window
    {
        public TextInputDialog(string title, string prompt, string defaultText = "")
        {
            InitializeComponent();
            DialogAnimationHelper.Register(this);
            Title = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultText ?? string.Empty;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        public string InputText => InputTextBox.Text?.Trim() ?? string.Empty;

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                AppMessageBox.Show("名称不能为空。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Ok_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
