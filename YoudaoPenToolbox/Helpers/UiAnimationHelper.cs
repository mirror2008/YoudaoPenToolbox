using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace YoudaoPenToolbox.Helpers
{
    public static class UiAnimationHelper
    {
        private static readonly Duration EntranceDuration = TimeSpan.FromMilliseconds(420);
        private static readonly Duration TabDuration = TimeSpan.FromMilliseconds(280);
        private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        public static void PlayWindowEntrance(FrameworkElement root, double fromY = 18)
        {
            if (root == null)
            {
                return;
            }

            root.Opacity = 0;
            var transform = new TranslateTransform(0, fromY);
            root.RenderTransform = transform;
            root.RenderTransformOrigin = new Point(0.5, 0.5);

            var opacity = new DoubleAnimation(0, 1, EntranceDuration) { EasingFunction = EaseOut };
            var slide = new DoubleAnimation(fromY, 0, EntranceDuration) { EasingFunction = EaseOut };

            root.BeginAnimation(UIElement.OpacityProperty, opacity);
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        public static void PlayTabContentIn(TabItem tab)
        {
            if (tab?.Content is not FrameworkElement content)
            {
                return;
            }

            content.Opacity = 0;
            var transform = new TranslateTransform(0, 10);
            content.RenderTransform = transform;
            content.RenderTransformOrigin = new Point(0.5, 0);

            var opacity = new DoubleAnimation(0, 1, TabDuration) { EasingFunction = EaseOut };
            var slide = new DoubleAnimation(10, 0, TabDuration) { EasingFunction = EaseOut };

            content.BeginAnimation(UIElement.OpacityProperty, opacity);
            transform.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        public static void AnimateThemeSegment(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            var scale = button.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1);
                button.RenderTransform = scale;
                button.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var target = isActive ? 1.04 : 1.0;
            var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = EaseOut
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }
}
