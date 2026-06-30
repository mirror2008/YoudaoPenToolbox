using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using YoudaoPenToolbox.Models;

namespace YoudaoPenToolbox.Helpers
{
    public static class ScreenMirrorCoordinateHelper
    {
        public static bool TryMapElementPointToDevice(
            FrameworkElement element,
            ImageSource frame,
            ScreenMirrorDisplayInfo displayInfo,
            Point elementPoint,
            out int deviceX,
            out int deviceY)
        {
            deviceX = 0;
            deviceY = 0;

            if (element == null || frame == null || displayInfo == null || !displayInfo.IsValid)
            {
                return false;
            }

            var sourceWidth = frame.Width;
            var sourceHeight = frame.Height;
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                sourceWidth = displayInfo.Width;
                sourceHeight = displayInfo.Height;
            }

            var renderWidth = element.ActualWidth;
            var renderHeight = element.ActualHeight;
            if (renderWidth <= 0 || renderHeight <= 0)
            {
                return false;
            }

            var scale = Math.Min(renderWidth / sourceWidth, renderHeight / sourceHeight);
            if (scale <= 0)
            {
                return false;
            }

            var displayedWidth = sourceWidth * scale;
            var displayedHeight = sourceHeight * scale;
            var offsetX = (renderWidth - displayedWidth) / 2.0;
            var offsetY = (renderHeight - displayedHeight) / 2.0;

            var relativeX = elementPoint.X - offsetX;
            var relativeY = elementPoint.Y - offsetY;
            if (relativeX < 0 || relativeY < 0 || relativeX > displayedWidth || relativeY > displayedHeight)
            {
                return false;
            }

            var displayX = (int)Math.Round(relativeX / scale);
            var displayY = (int)Math.Round(relativeY / scale);
            displayX = Clamp(displayX, 0, displayInfo.Width - 1);
            displayY = Clamp(displayY, 0, displayInfo.Height - 1);

            MapDisplayToTouch(displayInfo, displayX, displayY, out deviceX, out deviceY);
            return true;
        }

        public static void MapDisplayToTouch(
            ScreenMirrorDisplayInfo displayInfo,
            int displayX,
            int displayY,
            out int touchX,
            out int touchY)
        {
            switch (NormalizeDirection(displayInfo.TouchDirection))
            {
                case 90:
                    touchX = displayInfo.Height - 1 - displayY;
                    touchY = displayX;
                    break;
                case 180:
                    touchX = displayInfo.Width - 1 - displayX;
                    touchY = displayInfo.Height - 1 - displayY;
                    break;
                case 270:
                    touchX = displayY;
                    touchY = displayInfo.Width - 1 - displayX;
                    break;
                default:
                    touchX = displayX;
                    touchY = displayY;
                    break;
            }
        }

        public static ScreenMirrorDisplayInfo ParseMiniAppConfig(string json)
        {
            var info = new ScreenMirrorDisplayInfo();
            if (string.IsNullOrWhiteSpace(json))
            {
                return info;
            }

            info.Width = ReadJsonInt(json, "width", info.Width);
            info.Height = ReadJsonInt(json, "height", info.Height);
            info.TouchDirection = ReadJsonInt(json, "tp_direction", ReadJsonInt(json, "direction", info.TouchDirection));
            return info;
        }

        private static int NormalizeDirection(int direction)
        {
            var normalized = direction % 360;
            return normalized < 0 ? normalized + 360 : normalized;
        }

        private static int ReadJsonInt(string json, string key, int fallback)
        {
            var match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : fallback;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
