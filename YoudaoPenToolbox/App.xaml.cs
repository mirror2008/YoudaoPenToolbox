using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using YoudaoPenToolbox.Services;
using YoudaoPenToolbox.Views;

namespace YoudaoPenToolbox
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            if (await TryForceUpdateAsync().ConfigureAwait(true))
            {
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        private async Task<bool> TryForceUpdateAsync()
        {
            try
            {
                var updateService = new AppUpdateService();
                var check = await updateService.CheckForUpdateAsync().ConfigureAwait(true);
                if (check?.HasUpdate != true)
                {
                    return false;
                }

                var updateWindow = new UpdateWindow(check, updateService);
                updateWindow.ShowDialog();
                Shutdown();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowFatalError(e.Exception);
            e.Handled = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowFatalError(ex);
            }
        }

        private static void ShowFatalError(Exception ex)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}\r\n\r\n");
            }
            catch
            {

            }

            MessageBox.Show(
                $"程序发生错误:\r\n\r\n{ex.Message}\r\n\r\n详情已写入:\r\n{logPath}",
                "有道词典笔工具箱",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
