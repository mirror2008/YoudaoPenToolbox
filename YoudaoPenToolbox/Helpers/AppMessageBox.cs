using System.Windows;
using YoudaoPenToolbox.Views;

namespace YoudaoPenToolbox.Helpers
{
    public static class AppMessageBox
    {
        public static MessageBoxResult Show(
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
        {
            return Show(messageBoxText, caption, button, icon, ResolveOwner());
        }

        public static MessageBoxResult Show(
            string messageBoxText,
            string caption,
            MessageBoxButton button,
            MessageBoxImage icon,
            Window owner)
        {
            var dialog = new AppMessageBoxWindow(messageBoxText, caption, button, icon);
            if (owner != null && owner.IsLoaded)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
            return dialog.Result;
        }

        private static Window ResolveOwner()
        {
            var app = Application.Current;
            if (app?.MainWindow != null && app.MainWindow.IsLoaded)
            {
                return app.MainWindow;
            }

            return null;
        }
    }
}
