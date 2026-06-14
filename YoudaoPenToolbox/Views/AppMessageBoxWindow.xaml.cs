using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YoudaoPenToolbox.Helpers;

namespace YoudaoPenToolbox.Views
{
    public partial class AppMessageBoxWindow : Window
    {
        public AppMessageBoxWindow(
            string message,
            string caption,
            MessageBoxButton buttons,
            MessageBoxImage icon)
        {
            InitializeComponent();
            DialogAnimationHelper.Register(this);

            Result = MessageBoxResult.None;
            Title = string.IsNullOrWhiteSpace(caption) ? "提示" : caption;
            TitleText.Text = Title;
            MessageText.Text = message ?? string.Empty;

            ApplyIcon(icon);
            BuildButtons(buttons, icon);
        }

        public MessageBoxResult Result { get; private set; }

        private void ApplyIcon(MessageBoxImage icon)
        {
            if (icon == MessageBoxImage.None)
            {
                IconBadge.Visibility = Visibility.Collapsed;
                return;
            }

            IconBadge.Visibility = Visibility.Visible;

            if (icon == MessageBoxImage.Information)
            {
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(36, 0, 120, 212));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                IconText.Text = "i";
            }
            else if (icon == MessageBoxImage.Question)
            {
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(36, 64, 158, 255));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(64, 158, 255));
                IconText.Text = "?";
            }
            else if (icon == MessageBoxImage.Warning)
            {
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(36, 230, 162, 60));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(230, 162, 60));
                IconText.Text = "!";
            }
            else if (icon == MessageBoxImage.Hand)
            {
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(36, 245, 108, 108));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(245, 108, 108));
                IconText.Text = "×";
            }
            else
            {
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(36, 245, 108, 108));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(245, 108, 108));
                IconText.Text = "!";
            }
        }

        private void BuildButtons(MessageBoxButton buttons, MessageBoxImage icon)
        {
            var useDanger = icon == MessageBoxImage.Warning
                || icon == MessageBoxImage.Error
                || icon == MessageBoxImage.Stop;

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    AddButton("确定", MessageBoxResult.OK, isPrimary: true, useDanger: false, isDefault: true);
                    break;
                case MessageBoxButton.OKCancel:
                    AddButton("取消", MessageBoxResult.Cancel, isPrimary: false, useDanger: false, isDefault: false, isCancel: true);
                    AddButton("确定", MessageBoxResult.OK, isPrimary: true, useDanger: false, isDefault: true);
                    break;
                case MessageBoxButton.YesNo:
                    AddButton("否", MessageBoxResult.No, isPrimary: false, useDanger: false, isDefault: false, isCancel: true);
                    AddButton("是", MessageBoxResult.Yes, isPrimary: true, useDanger: useDanger, isDefault: true);
                    break;
                case MessageBoxButton.YesNoCancel:
                    AddButton("取消", MessageBoxResult.Cancel, isPrimary: false, useDanger: false, isDefault: false, isCancel: true);
                    AddButton("否", MessageBoxResult.No, isPrimary: false, useDanger: false, isDefault: false);
                    AddButton("是", MessageBoxResult.Yes, isPrimary: true, useDanger: useDanger, isDefault: true);
                    break;
                default:
                    AddButton("确定", MessageBoxResult.OK, isPrimary: true, useDanger: false, isDefault: true);
                    break;
            }
        }

        private void AddButton(
            string text,
            MessageBoxResult result,
            bool isPrimary,
            bool useDanger,
            bool isDefault,
            bool isCancel = false)
        {
            var button = new Button
            {
                Content = text,
                Width = 100,
                Height = 38,
                Margin = new Thickness(10, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = isCancel
            };

            if (isPrimary)
            {
                button.Style = (Style)FindResource(useDanger ? "DialogDangerButton" : "PrimaryActionButton");
            }

            button.Click += (_, __) =>
            {
                Result = result;
                Close();
            };

            ButtonPanel.Children.Add(button);
        }
    }
}
