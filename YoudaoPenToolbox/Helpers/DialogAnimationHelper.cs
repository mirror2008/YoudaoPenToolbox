using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace YoudaoPenToolbox.Helpers
{
    public static class DialogAnimationHelper
    {
        private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(420);
        private static readonly TimeSpan CloseDuration = TimeSpan.FromMilliseconds(280);
        private static readonly TimeSpan PanelDuration = TimeSpan.FromMilliseconds(300);
        private static readonly IEasingFunction OpenEase = new PowerEase { Power = 4, EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction CloseEase = new CubicEase { EasingMode = EasingMode.EaseIn };
        private static readonly IEasingFunction OpenScaleEase = new BackEase { Amplitude = 0.28, EasingMode = EasingMode.EaseOut };
        private const string DialogRootName = "DialogRoot";

        public static void Register(Window window, Func<bool> canClose = null)
        {
            if (window == null)
            {
                return;
            }

            var state = new DialogAnimationState();
            window.Tag = state;

            window.Loaded += (_, __) =>
            {
                var root = FindRoot(window);
                PlayOpen(window, root);
            };

            window.Closing += (_, e) =>
            {
                if (canClose != null && !canClose())
                {
                    e.Cancel = true;
                    return;
                }

                if (state.IsClosingAnimated)
                {
                    return;
                }

                state.PendingDialogResult = window.DialogResult;
                e.Cancel = true;
                var root = FindRoot(window);
                PlayClose(window, root, () =>
                {
                    state.IsClosingAnimated = true;
                    if (state.PendingDialogResult.HasValue)
                    {
                        window.DialogResult = state.PendingDialogResult;
                    }

                    window.Close();
                });
            };
        }

        public static void TransitionPanels(UIElement hide, UIElement show)
        {
            if (hide == null || show == null)
            {
                return;
            }

            show.Opacity = 0;
            show.Visibility = Visibility.Visible;

            var hideFade = new DoubleAnimation(1, 0, PanelDuration) { EasingFunction = CloseEase };
            hideFade.Completed += (_, __) => hide.Visibility = Visibility.Collapsed;

            var showFade = new DoubleAnimation(0, 1, PanelDuration) { EasingFunction = OpenEase };
            var showSlide = new DoubleAnimation(14, 0, PanelDuration) { EasingFunction = OpenScaleEase };

            PrepareSlideTransform(show, 14);
            hide.BeginAnimation(UIElement.OpacityProperty, hideFade);
            show.BeginAnimation(UIElement.OpacityProperty, showFade);
            GetTranslate(show)?.BeginAnimation(TranslateTransform.YProperty, showSlide);
        }

        private static FrameworkElement FindRoot(Window window)
        {
            if (window.FindName(DialogRootName) is FrameworkElement namedRoot)
            {
                return namedRoot;
            }

            return window.Content as FrameworkElement;
        }

        private static void PlayOpen(Window window, FrameworkElement root)
        {
            if (root == null)
            {
                window.Opacity = 0;
                window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, OpenDuration)
                {
                    EasingFunction = OpenEase
                });
                return;
            }

            window.Opacity = 0;
            root.Opacity = 0;
            var transform = CreateTransformGroup(0.88, 20);
            root.RenderTransform = transform;
            root.RenderTransformOrigin = new Point(0.5, 0.5);

            window.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, OpenDuration)
            {
                EasingFunction = OpenEase
            });
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, OpenDuration)
            {
                EasingFunction = OpenEase
            });
            AnimateScale(transform, 0.88, 1.0, OpenDuration, OpenScaleEase);
            AnimateTranslateY(transform, 20, 0, OpenDuration, OpenEase);
        }

        private static void PlayClose(Window window, FrameworkElement root, Action onComplete)
        {
            if (root == null)
            {
                var fade = new DoubleAnimation(1, 0, CloseDuration) { EasingFunction = CloseEase };
                fade.Completed += (_, __) => onComplete?.Invoke();
                window.BeginAnimation(UIElement.OpacityProperty, fade);
                return;
            }

            var transform = root.RenderTransform as TransformGroup ?? CreateTransformGroup(1.0, 0);

            var windowFade = new DoubleAnimation(1, 0, CloseDuration) { EasingFunction = CloseEase };
            var rootFade = new DoubleAnimation(1, 0, CloseDuration) { EasingFunction = CloseEase };
            windowFade.Completed += (_, __) => onComplete?.Invoke();

            window.BeginAnimation(UIElement.OpacityProperty, windowFade);
            root.BeginAnimation(UIElement.OpacityProperty, rootFade);
            AnimateScale(transform, 1.0, 0.94, CloseDuration, CloseEase);
            AnimateTranslateY(transform, 0, 10, CloseDuration, CloseEase);
        }

        private static TransformGroup CreateTransformGroup(double scale, double translateY)
        {
            return new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(scale, scale),
                    new TranslateTransform(0, translateY)
                }
            };
        }

        private static void PrepareSlideTransform(UIElement element, double fromY)
        {
            var transform = GetTranslate(element);
            if (transform == null)
            {
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(1, 1));
                group.Children.Add(new TranslateTransform(0, fromY));
                element.RenderTransform = group;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                return;
            }

            transform.Y = fromY;
        }

        private static TranslateTransform GetTranslate(UIElement element)
        {
            if (element.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is TranslateTransform translate)
                    {
                        return translate;
                    }
                }

                var created = new TranslateTransform(0, 0);
                group.Children.Add(created);
                return created;
            }

            if (element.RenderTransform is TranslateTransform direct)
            {
                return direct;
            }

            return null;
        }

        private static void AnimateScale(
            TransformGroup group,
            double from,
            double to,
            TimeSpan duration,
            IEasingFunction easing)
        {
            if (group?.Children.Count == 0 || group.Children[0] is not ScaleTransform scale)
            {
                return;
            }

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(from, to, duration)
            {
                EasingFunction = easing
            });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(from, to, duration)
            {
                EasingFunction = easing
            });
        }

        private static void AnimateTranslateY(
            TransformGroup group,
            double from,
            double to,
            TimeSpan duration,
            IEasingFunction easing)
        {
            TranslateTransform translate = null;
            if (group != null)
            {
                foreach (var child in group.Children)
                {
                    if (child is TranslateTransform found)
                    {
                        translate = found;
                        break;
                    }
                }
            }

            if (translate == null)
            {
                return;
            }

            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(from, to, duration)
            {
                EasingFunction = easing
            });
        }

        private sealed class DialogAnimationState
        {
            public bool IsClosingAnimated { get; set; }
            public bool? PendingDialogResult { get; set; }
        }
    }
}
