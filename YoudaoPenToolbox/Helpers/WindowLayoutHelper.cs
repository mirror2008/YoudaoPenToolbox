using System;
using System.Windows;
using System.Windows.Media;

namespace YoudaoPenToolbox.Helpers
{
    public static class WindowLayoutHelper
    {
        public const double DesignWidth = 1440;
        public const double DesignHeight = 940;
        public const double ShadowPadding = 16;
        public const double MinContentWidth = 960;
        public const double MinContentHeight = 640;

        public static double WindowAspectRatio =>
            (DesignWidth + ShadowPadding) / (DesignHeight + ShadowPadding);

        public static void ApplyInitialWindowBounds(Window window)
        {
            if (window == null)
            {
                return;
            }

            ApplyWorkAreaLimits(window);

            var work = SystemParameters.WorkArea;
            var targetWidth = DesignWidth + ShadowPadding;
            var targetHeight = DesignHeight + ShadowPadding;
            var aspect = WindowAspectRatio;

            var width = Math.Min(targetWidth, work.Width);
            var height = width / aspect;

            if (height > work.Height)
            {
                height = Math.Min(targetHeight, work.Height);
                width = height * aspect;
            }

            window.Width = width;
            window.Height = height;
            window.Left = work.Left + Math.Max(0, (work.Width - width) / 2);
            window.Top = work.Top + Math.Max(0, (work.Height - height) / 2);
        }

        public static void ApplyWorkAreaLimits(Window window)
        {
            if (window == null)
            {
                return;
            }

            var work = SystemParameters.WorkArea;
            window.MinWidth = MinContentWidth + ShadowPadding;
            window.MinHeight = MinContentHeight + ShadowPadding;
            window.MaxWidth = work.Width;
            window.MaxHeight = work.Height;

            if (window.Width > window.MaxWidth)
            {
                window.Width = window.MaxWidth;
                window.Height = window.Width / WindowAspectRatio;
            }

            if (window.Height > window.MaxHeight)
            {
                window.Height = window.MaxHeight;
                window.Width = window.Height * WindowAspectRatio;
            }
        }

        public static void EnforceAspectRatio(Window window)
        {
            if (window == null || window.WindowState != WindowState.Normal)
            {
                return;
            }

            var aspect = WindowAspectRatio;
            if (window.ActualWidth <= 0 || window.ActualHeight <= 0)
            {
                return;
            }

            var currentAspect = window.ActualWidth / window.ActualHeight;
            if (Math.Abs(currentAspect - aspect) <= 0.005)
            {
                return;
            }

            window.Height = window.ActualWidth / aspect;

            if (window.Height > window.MaxHeight)
            {
                window.Height = window.MaxHeight;
                window.Width = window.Height * aspect;
            }

            if (window.Width > window.MaxWidth)
            {
                window.Width = window.MaxWidth;
                window.Height = window.Width / aspect;
            }
        }

        public static double GetUniformScale(double contentWidth, double contentHeight)
        {
            if (contentWidth <= 0 || contentHeight <= 0)
            {
                return 1;
            }

            var scaleW = contentWidth / DesignWidth;
            var scaleH = contentHeight / DesignHeight;
            return Math.Min(scaleW, scaleH);
        }

        public static void ApplyDpiAwareTextOptions(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            TextOptions.SetTextFormattingMode(root, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(root, TextRenderingMode.ClearType);
        }
    }
}
