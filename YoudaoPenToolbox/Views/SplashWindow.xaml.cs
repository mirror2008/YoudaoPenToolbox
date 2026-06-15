using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using YoudaoPenToolbox.Helpers;

namespace YoudaoPenToolbox.Views
{
    public partial class SplashWindow : Window
    {
        private const int ShatterColumns = 12;
        private const int ShatterRows = 8;

        private static readonly TimeSpan ShatterDuration = TimeSpan.FromMilliseconds(820);
        private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        private static readonly IEasingFunction ShatterEase = new CubicEase { EasingMode = EasingMode.EaseIn };

        private Storyboard _ambientStoryboard;

        public SplashWindow()
        {
            InitializeComponent();
            Loaded += SplashWindow_Loaded;
        }

        public void SetStatus(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(text));
                return;
            }

            StatusText.Text = text ?? string.Empty;
        }

        private void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowLayoutHelper.ApplyInitialWindowBounds(this);
            PlayEntranceAnimation();
            StartAmbientBreathing();
            StartSpinnerRotation();
        }

        private void PlayEntranceAnimation()
        {
            var cardFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(720)) { EasingFunction = EaseOut };
            HeroCard.BeginAnimation(UIElement.OpacityProperty, cardFade);

            var cardSlide = new DoubleAnimation(32, 0, TimeSpan.FromMilliseconds(820)) { EasingFunction = EaseOut };
            SetTransformAnimation(HeroCard, TranslateTransform.YProperty, cardSlide, 1);

            var cardScaleX = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(820))
            {
                EasingFunction = new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut }
            };
            var cardScaleY = cardScaleX.Clone();
            SetTransformAnimation(HeroCard, ScaleTransform.ScaleXProperty, cardScaleX, 0);
            SetTransformAnimation(HeroCard, ScaleTransform.ScaleYProperty, cardScaleY, 0);

            var subtitleFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
            {
                BeginTime = TimeSpan.FromMilliseconds(280),
                EasingFunction = EaseOut
            };
            SubtitleText.BeginAnimation(UIElement.OpacityProperty, subtitleFade);

            var spinnerFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(480))
            {
                BeginTime = TimeSpan.FromMilliseconds(420),
                EasingFunction = EaseOut
            };
            LoadingSpinner.BeginAnimation(UIElement.OpacityProperty, spinnerFade);
            StatusText.BeginAnimation(UIElement.OpacityProperty, spinnerFade);

            var footerFade = new DoubleAnimation(0, 0.75, TimeSpan.FromMilliseconds(520))
            {
                BeginTime = TimeSpan.FromMilliseconds(520),
                EasingFunction = EaseOut
            };
            FooterText.BeginAnimation(UIElement.OpacityProperty, footerFade);

            var logoPulseX = new DoubleAnimation(1, 1.06, TimeSpan.FromMilliseconds(1200))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var logoPulseY = logoPulseX.Clone();
            LogoBadge.RenderTransform = new ScaleTransform(1, 1);
            LogoBadge.RenderTransformOrigin = new Point(0.5, 0.5);
            LogoBadge.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, logoPulseX);
            LogoBadge.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, logoPulseY);
        }

        private static void SetTransformAnimation(
            FrameworkElement target,
            DependencyProperty property,
            AnimationTimeline animation,
            int groupChildIndex)
        {
            if (target.RenderTransform is TransformGroup group && group.Children.Count > groupChildIndex)
            {
                group.Children[groupChildIndex].BeginAnimation(property, animation);
            }
        }

        private void StartAmbientBreathing()
        {
            AmbientGlow.RenderTransform = new ScaleTransform(1, 1);
            AmbientGlow.RenderTransformOrigin = new Point(0.5, 0.5);

            _ambientStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

            var scaleX = new DoubleAnimation(0.92, 1.08, TimeSpan.FromSeconds(5.5))
            {
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var scaleY = scaleX.Clone();
            Storyboard.SetTarget(scaleX, AmbientGlow);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            Storyboard.SetTarget(scaleY, AmbientGlow);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            _ambientStoryboard.Children.Add(scaleX);
            _ambientStoryboard.Children.Add(scaleY);

            var glowFade = new DoubleAnimation(0.10, 0.18, TimeSpan.FromSeconds(5.5))
            {
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(glowFade, AmbientGlow);
            Storyboard.SetTargetProperty(glowFade, new PropertyPath(UIElement.OpacityProperty));
            _ambientStoryboard.Children.Add(glowFade);

            _ambientStoryboard.Begin();
        }

        private void StartSpinnerRotation()
        {
            var rotate = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.25))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            SpinnerArc.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, rotate);
        }

        public async Task PlayExitTransitionAsync(Window mainWindow)
        {
            StopIdleAnimations();
            UpdateLayout();

            var width = Math.Max(1, (int)Math.Ceiling(ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(ActualHeight));
            var snapshot = CaptureSnapshot(width, height);

            ContentLayer.Visibility = Visibility.Collapsed;
            ShatterCanvas.Visibility = Visibility.Visible;
            ShatterCanvas.Width = width;
            ShatterCanvas.Height = height;

            var cellWidth = width / (double)ShatterColumns;
            var cellHeight = height / (double)ShatterRows;
            var centerX = width / 2.0;
            var centerY = height / 2.0;
            var maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY);
            var random = new Random(7);
            var master = new Storyboard();
            var totalDuration = ShatterDuration + TimeSpan.FromMilliseconds(120);

            for (var row = 0; row < ShatterRows; row++)
            {
                for (var col = 0; col < ShatterColumns; col++)
                {
                    var piece = CreateShard(snapshot, col, row, cellWidth, cellHeight);
                    Canvas.SetLeft(piece, col * cellWidth);
                    Canvas.SetTop(piece, row * cellHeight);
                    ShatterCanvas.Children.Add(piece);

                    var pieceCenterX = col * cellWidth + cellWidth / 2.0;
                    var pieceCenterY = row * cellHeight + cellHeight / 2.0;
                    var dirX = pieceCenterX - centerX;
                    var dirY = pieceCenterY - centerY;
                    var length = Math.Sqrt(dirX * dirX + dirY * dirY);
                    if (length < 0.001)
                    {
                        dirX = random.NextDouble() - 0.5;
                        dirY = random.NextDouble() - 0.5;
                        length = Math.Sqrt(dirX * dirX + dirY * dirY);
                    }

                    dirX /= length;
                    dirY /= length;

                    var ripple = length / maxDistance;
                    var distance = 100 + ripple * 100 + random.NextDouble() * 80;
                    var translateX = dirX * distance + (random.NextDouble() - 0.5) * 30;
                    var translateY = dirY * distance + 60 + random.NextDouble() * 50;
                    var rotation = (random.NextDouble() - 0.5) * 60;
                    var delay = TimeSpan.FromMilliseconds(ripple * 130 + random.Next(0, 40));

                    AddShardAnimation(master, piece, translateX, translateY, rotation, delay);
                }
            }

            if (mainWindow != null)
            {
                mainWindow.Opacity = 0;
                var mainFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(480))
                {
                    BeginTime = TimeSpan.FromMilliseconds(260),
                    EasingFunction = EaseOut
                };
                Storyboard.SetTarget(mainFade, mainWindow);
                Storyboard.SetTargetProperty(mainFade, new PropertyPath(UIElement.OpacityProperty));
                master.Children.Add(mainFade);
            }

            var completion = new TaskCompletionSource<bool>();
            master.Duration = new Duration(totalDuration);
            master.Completed += (_, __) => completion.TrySetResult(true);
            master.Begin();

            await Task.WhenAny(completion.Task, Task.Delay(totalDuration + TimeSpan.FromMilliseconds(100)))
                .ConfigureAwait(true);
            Close();
        }

        private void StopIdleAnimations()
        {
            _ambientStoryboard?.Stop();
            SpinnerArc.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private static Border CreateShard(
            RenderTargetBitmap snapshot,
            int col,
            int row,
            double cellWidth,
            double cellHeight)
        {
            var brush = new ImageBrush(snapshot)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = new Rect(
                    col / (double)ShatterColumns,
                    row / (double)ShatterRows,
                    1.0 / ShatterColumns,
                    1.0 / ShatterRows)
            };

            return new Border
            {
                Width = cellWidth,
                Height = cellHeight,
                Background = brush,
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
        }

        private static void AddShardAnimation(
            Storyboard master,
            UIElement piece,
            double translateX,
            double translateY,
            double rotation,
            TimeSpan delay)
        {
            var transform = new TransformGroup
            {
                Children =
                {
                    new TranslateTransform(),
                    new RotateTransform()
                }
            };
            piece.RenderTransform = transform;

            var duration = new Duration(ShatterDuration);

            var moveX = new DoubleAnimation(0, translateX, duration)
            {
                EasingFunction = ShatterEase,
                BeginTime = delay
            };
            Storyboard.SetTarget(moveX, piece);
            Storyboard.SetTargetProperty(moveX, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(TranslateTransform.X)"));
            master.Children.Add(moveX);

            var moveY = new DoubleAnimation(0, translateY, duration)
            {
                EasingFunction = ShatterEase,
                BeginTime = delay
            };
            Storyboard.SetTarget(moveY, piece);
            Storyboard.SetTargetProperty(moveY, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(TranslateTransform.Y)"));
            master.Children.Add(moveY);

            var spin = new DoubleAnimation(0, rotation, duration)
            {
                EasingFunction = ShatterEase,
                BeginTime = delay
            };
            Storyboard.SetTarget(spin, piece);
            Storyboard.SetTargetProperty(spin, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(RotateTransform.Angle)"));
            master.Children.Add(spin);

            var fade = new DoubleAnimation(1, 0, duration)
            {
                EasingFunction = ShatterEase,
                BeginTime = delay
            };
            Storyboard.SetTarget(fade, piece);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            master.Children.Add(fade);
        }

        private RenderTargetBitmap CaptureSnapshot(int width, int height)
        {
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(ContentLayer);
            return bitmap;
        }
    }
}
