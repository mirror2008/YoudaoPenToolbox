using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace YoudaoPenToolbox.Helpers
{
    public static class WindowChromeHelper
    {
        private const int GwlStyle = -16;
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwcpRound = 2;

        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const long WsMinimizeBox = 0x00020000L;
        private const long WsMaximizeBox = 0x00010000L;
        private const long WsSysMenu = 0x00080000L;
        private const long WsBorder = 0x00800000L;

        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpNomove = 0x0002;
        private const uint SwpNosize = 0x0001;
        private const uint SwpNozorder = 0x0004;

        public static void Attach(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.CanMinimize;
            window.Background = Brushes.Transparent;
            window.AllowsTransparency = true;
            window.ShowInTaskbar = true;

            if (window.IsLoaded)
            {
                ApplyNativeChrome(window);
            }
            else
            {
                window.SourceInitialized += OnSourceInitialized;
                window.Loaded += OnLoaded;
            }
        }

        public static void TryDragWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                window.DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void OnSourceInitialized(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                ApplyNativeChrome(window);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                window.Loaded -= OnLoaded;
                ApplyNativeChrome(window);
            }
        }

        private static void ApplyNativeChrome(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            window.WindowStyle = WindowStyle.None;

            var style = GetWindowLongPtr(handle, GwlStyle).ToInt64();
            style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu | WsBorder);
            SetWindowLongPtr(handle, GwlStyle, new IntPtr(style));

            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                SwpNomove | SwpNosize | SwpNozorder | SwpFrameChanged);

            try
            {
                var round = DwmwcpRound;
                _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref round, sizeof(int));
            }
            catch
            {
            }
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            }
            else
            {
                SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}
