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

        public static void ApplyMainWindowBounds(Window window)
        {
            if (window == null)
            {
                return;
            }

            var work = SystemParameters.WorkArea;
            var maxContentWidth = Math.Max(960, work.Width - ShadowPadding);
            var maxContentHeight = Math.Max(640, work.Height - ShadowPadding);

            var contentWidth = Math.Min(DesignWidth, maxContentWidth);
            var contentHeight = Math.Min(DesignHeight, maxContentHeight);

            window.MinWidth = Math.Min(960 + ShadowPadding, maxContentWidth + ShadowPadding);
            window.MinHeight = Math.Min(640 + ShadowPadding, maxContentHeight + ShadowPadding);
            window.MaxWidth = work.Width;
            window.MaxHeight = work.Height;
            window.Width = contentWidth + ShadowPadding;
            window.Height = contentHeight + ShadowPadding;
            window.Left = work.Left + Math.Max(0, (work.Width - window.Width) / 2);
            window.Top = work.Top + Math.Max(0, (work.Height - window.Height) / 2);
        }

        public static double GetContentScale(double contentWidth, double contentHeight)
        {
            if (contentWidth <= 0 || contentHeight <= 0)
            {
                return 1;
            }

            var scaleW = contentWidth / DesignWidth;
            var scaleH = contentHeight / DesignHeight;
            return Math.Min(1, Math.Min(scaleW, scaleH));
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
