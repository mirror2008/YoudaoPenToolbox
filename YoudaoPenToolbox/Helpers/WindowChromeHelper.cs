using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using System.Windows;

using System.Windows.Interop;

using System.Windows.Media;



namespace YoudaoPenToolbox.Helpers

{

    public static class WindowChromeHelper

    {

        private const int GwlStyle = -16;

        private const int WmNchitTest = 0x0084;

        private const int WmSizing = 0x0214;

        private const int WmEnterSizeMove = 0x0231;

        private const int WmExitSizeMove = 0x0232;



        private const int HtCaption = 2;

        private const int HtTopLeft = 13;

        private const int HtTopRight = 14;

        private const int HtBottomLeft = 16;

        private const int HtBottomRight = 17;



        private const int WmszTopLeft = 4;

        private const int WmszTopRight = 5;

        private const int WmszBottomLeft = 7;

        private const int WmszBottomRight = 8;



        private const int WmSysCommand = 0x0112;
        private const int ScMaximize = 0xF030;

        private const int DwmwaWindowCornerPreference = 33;

        private const int DwmwcpDoNotRound = 1;



        private const long WsCaption = 0x00C00000L;

        private const long WsMinimizeBox = 0x00020000L;

        private const long WsMaximizeBox = 0x00010000L;

        private const long WsSysMenu = 0x00080000L;

        private const long WsBorder = 0x00800000L;



        private const uint SwpFrameChanged = 0x0020;

        private const uint SwpNomove = 0x0002;

        private const uint SwpNosize = 0x0001;

        private const uint SwpNozorder = 0x0004;



        private const double CornerResizeThickness = 16;



        private static readonly Dictionary<IntPtr, WindowChromeState> HandleStates =

            new Dictionary<IntPtr, WindowChromeState>();



        public const double TitleBarHeight = 36;



        public static void Attach(Window window, Action<bool> onSizeMoveChanged = null)

        {

            if (window == null)

            {

                return;

            }



            window.WindowStyle = WindowStyle.None;

            window.ResizeMode = ResizeMode.CanResize;

            window.Background = Brushes.Transparent;

            window.AllowsTransparency = true;

            window.ShowInTaskbar = true;



            if (window.IsLoaded)

            {

                ApplyNativeChrome(window, onSizeMoveChanged);

            }

            else
            {
                void OnSourceInitialized(object sender, EventArgs e)
                {
                    window.SourceInitialized -= OnSourceInitialized;
                    ApplyNativeChrome(window, onSizeMoveChanged);
                }

                window.SourceInitialized += OnSourceInitialized;
            }

        }



        private static void ApplyNativeChrome(Window window, Action<bool> onSizeMoveChanged)

        {

            var helper = new WindowInteropHelper(window);

            var handle = helper.Handle;

            if (handle == IntPtr.Zero)

            {

                return;

            }



            window.WindowStyle = WindowStyle.None;

            window.ResizeMode = ResizeMode.CanResize;



            HandleStates[handle] = new WindowChromeState(window, onSizeMoveChanged);

            window.Closed += (_, __) => HandleStates.Remove(handle);



            var style = GetWindowLongPtr(handle, GwlStyle).ToInt64();

            style &= ~(WsCaption | WsMinimizeBox | WsMaximizeBox | WsSysMenu | WsBorder);

            SetWindowLongPtr(handle, GwlStyle, new IntPtr(style));



            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,

                SwpNomove | SwpNosize | SwpNozorder | SwpFrameChanged);



            var source = HwndSource.FromHwnd(handle);

            if (source != null)

            {

                source.RemoveHook(WndProc);

                source.AddHook(WndProc);

            }



            try

            {

                var cornerPreference = DwmwcpDoNotRound;

                _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));

            }

            catch

            {

            }

        }



        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)

        {

            if (!HandleStates.TryGetValue(hwnd, out var state) || state.Window == null || !state.Window.IsLoaded)

            {

                return IntPtr.Zero;

            }



            switch (msg)

            {

                case WmNchitTest:

                    return HandleNchitTest(state.Window, lParam, ref handled);

                case WmSizing:

                    HandleSizing(state.Window, wParam, lParam);

                    handled = true;

                    return IntPtr.Zero;

                case WmEnterSizeMove:

                    state.OnSizeMoveChanged?.Invoke(true);

                    return IntPtr.Zero;

                case WmExitSizeMove:

                    state.OnSizeMoveChanged?.Invoke(false);

                    return IntPtr.Zero;

                case WmSysCommand:

                    if ((wParam.ToInt32() & 0xFFF0) == ScMaximize)

                    {

                        handled = true;

                    }

                    return IntPtr.Zero;

            }



            return IntPtr.Zero;

        }



        private static IntPtr HandleNchitTest(Window window, IntPtr lParam, ref bool handled)

        {

            var screenX = (short)(lParam.ToInt64() & 0xFFFF);

            var screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            var point = window.PointFromScreen(new Point(screenX, screenY));



            var cornerHit = HitTestCorner(window, point);

            if (cornerHit != 0)

            {

                handled = true;

                return (IntPtr)cornerHit;

            }



            if (HitTestCaption(window, point))

            {

                handled = true;

                return (IntPtr)HtCaption;

            }



            return IntPtr.Zero;

        }



        private static int HitTestCorner(Window window, Point point)

        {

            var width = window.ActualWidth;

            var height = window.ActualHeight;

            if (width <= 0 || height <= 0)

            {

                return 0;

            }



            var left = point.X <= CornerResizeThickness;

            var right = point.X >= width - CornerResizeThickness;

            var top = point.Y <= CornerResizeThickness;

            var bottom = point.Y >= height - CornerResizeThickness;



            if (left && top)

            {

                return HtTopLeft;

            }



            if (right && top)

            {

                return HtTopRight;

            }



            if (left && bottom)

            {

                return HtBottomLeft;

            }



            if (right && bottom)

            {

                return HtBottomRight;

            }



            return 0;

        }



        private static void HandleSizing(Window window, IntPtr wParam, IntPtr lParam)

        {

            var edge = wParam.ToInt32();

            if (edge != WmszTopLeft && edge != WmszTopRight &&

                edge != WmszBottomLeft && edge != WmszBottomRight)

            {

                return;

            }



            var rc = Marshal.PtrToStructure<RectL>(lParam);

            EnforceAspectRatio(window, edge, ref rc);

            Marshal.StructureToPtr(rc, lParam, false);

        }



        private static void EnforceAspectRatio(Window window, int edge, ref RectL rc)

        {

            var aspect = WindowLayoutHelper.WindowAspectRatio;

            var scale = GetDpiScale(window);



            var minWidth = Math.Max(1, (int)Math.Round(window.MinWidth * scale));

            var minHeight = Math.Max(1, (int)Math.Round(window.MinHeight * scale));

            var maxWidth = Math.Max(minWidth, (int)Math.Round(window.MaxWidth * scale));

            var maxHeight = Math.Max(minHeight, (int)Math.Round(window.MaxHeight * scale));



            var width = rc.Right - rc.Left;

            var height = (int)Math.Round(width / aspect);



            switch (edge)

            {

                case WmszTopLeft:

                    rc.Top = rc.Bottom - height;

                    break;

                case WmszTopRight:

                    rc.Top = rc.Bottom - height;

                    break;

                case WmszBottomLeft:

                    rc.Bottom = rc.Top + height;

                    break;

                case WmszBottomRight:

                    rc.Bottom = rc.Top + height;

                    break;

            }



            width = rc.Right - rc.Left;

            height = rc.Bottom - rc.Top;



            if (width < minWidth)

            {

                width = minWidth;

                height = (int)Math.Round(width / aspect);

                SetRectSize(edge, ref rc, width, height);

            }

            else if (width > maxWidth)

            {

                width = maxWidth;

                height = (int)Math.Round(width / aspect);

                SetRectSize(edge, ref rc, width, height);

            }



            width = rc.Right - rc.Left;

            height = rc.Bottom - rc.Top;



            if (height < minHeight)

            {

                height = minHeight;

                width = (int)Math.Round(height * aspect);

                SetRectSize(edge, ref rc, width, height);

            }

            else if (height > maxHeight)

            {

                height = maxHeight;

                width = (int)Math.Round(height * aspect);

                SetRectSize(edge, ref rc, width, height);

            }

        }



        private static void SetRectSize(int edge, ref RectL rc, int width, int height)

        {

            switch (edge)

            {

                case WmszTopLeft:

                    rc.Left = rc.Right - width;

                    rc.Top = rc.Bottom - height;

                    break;

                case WmszTopRight:

                    rc.Right = rc.Left + width;

                    rc.Top = rc.Bottom - height;

                    break;

                case WmszBottomLeft:

                    rc.Left = rc.Right - width;

                    rc.Bottom = rc.Top + height;

                    break;

                case WmszBottomRight:

                    rc.Right = rc.Left + width;

                    rc.Bottom = rc.Top + height;

                    break;

            }

        }



        private static double GetDpiScale(Window window)

        {

            var source = PresentationSource.FromVisual(window) as HwndSource;

            return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        }



        private static bool HitTestCaption(Window window, Point point)

        {

            var width = window.ActualWidth;

            var height = window.ActualHeight;

            if (width <= 0 || height <= 0)

            {

                return false;

            }



            var shellInset = WindowLayoutHelper.ShadowPadding / 2;

            var titleTop = shellInset;

            var titleBottom = shellInset + GetScaledTitleBarHeight(window);

            var titleRight = width - shellInset - 110;



            return point.Y >= titleTop && point.Y <= titleBottom &&

                   point.X >= shellInset && point.X <= titleRight;

        }



        private static double GetScaledTitleBarHeight(Window window)

        {

            var contentWidth = Math.Max(1, window.ActualWidth - WindowLayoutHelper.ShadowPadding);

            var contentHeight = Math.Max(1, window.ActualHeight - WindowLayoutHelper.ShadowPadding);

            var scale = Math.Min(

                contentWidth / WindowLayoutHelper.DesignWidth,

                contentHeight / WindowLayoutHelper.DesignHeight);

            return TitleBarHeight * scale;

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



        [StructLayout(LayoutKind.Sequential)]

        private struct RectL

        {

            public int Left;

            public int Top;

            public int Right;

            public int Bottom;

        }



        private sealed class WindowChromeState

        {

            public WindowChromeState(Window window, Action<bool> onSizeMoveChanged)

            {

                Window = window;

                OnSizeMoveChanged = onSizeMoveChanged;

            }



            public Window Window { get; }

            public Action<bool> OnSizeMoveChanged { get; }

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


