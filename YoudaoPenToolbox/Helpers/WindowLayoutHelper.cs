using System;
using System.Windows;

namespace YoudaoPenToolbox.Helpers
{
    public static class WindowLayoutHelper
    {
        public const double DesignWidth = 1440;
        public const double DesignHeight = 940;

        public static void ApplyMainWindowBounds(Window window)
        {
            if (window == null)
            {
                return;
            }

            var work = SystemParameters.WorkArea;
            var width = Math.Min(DesignWidth, work.Width);
            var height = Math.Min(DesignHeight, work.Height);

            window.MinWidth = Math.Min(1024, width);
            window.MinHeight = Math.Min(680, height);
            window.MaxWidth = work.Width;
            window.MaxHeight = work.Height;
            window.Width = width;
            window.Height = height;
            window.Left = work.Left + Math.Max(0, (work.Width - width) / 2);
            window.Top = work.Top + Math.Max(0, (work.Height - height) / 2);
        }
    }
}
