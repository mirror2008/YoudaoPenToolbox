using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Services;
using YoudaoPenToolbox.Views;

namespace YoudaoPenToolbox
{
    public partial class App : Application
    {
        private static readonly TimeSpan SplashMinimumDuration = TimeSpan.FromSeconds(3);

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            AppThemeService.Instance.Initialize();

            if (await TryForceUpdateAsync().ConfigureAwait(true))
            {
                return;
            }

            await LaunchWithSplashAsync().ConfigureAwait(true);
        }

        private static async Task LaunchWithSplashAsync()
        {
            var splash = new SplashWindow();
            splash.Show();

            try
            {
                splash.SetStatus("正在准备运行组件...");
                var bootstrap = new EmbeddedRuntimeBootstrapService();
                var progress = new Progress<string>(splash.SetStatus);
                await bootstrap.EnsureToolsAsync(progress).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                splash.Close();
                AppMessageBox.Show(
                    $"无法准备必要运行组件:\r\n\r\n{ex.Message}",
                    "有道词典笔工具箱",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            splash.SetStatus("正在启动...");

            var mainWindow = new MainWindow(skipEntranceAnimation: true)
            {
                ShowActivated = false,
                Opacity = 0
            };
            Current.MainWindow = mainWindow;
            mainWindow.Show();

            var initTask = mainWindow.InitializeApplicationAsync();
            var delayTask = Task.Delay(SplashMinimumDuration);
            await Task.WhenAll(initTask, delayTask).ConfigureAwait(true);

            await splash.PlayExitTransitionAsync(mainWindow).ConfigureAwait(true);

            mainWindow.ShowActivated = true;
            mainWindow.Activate();
            mainWindow.Focus();
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
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\r\n{ex}\r\n\r\n");
            }
            catch
            {
            }

            AppMessageBox.Show(
                $"程序发生错误:\r\n\r\n{ex.Message}\r\n\r\n详情已写入:\r\n{logPath}",
                "有道词典笔工具箱",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
