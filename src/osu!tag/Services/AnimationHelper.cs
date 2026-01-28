using System;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Osutag.Services
{
    public static class AnimationHelper
    {
        private static readonly ConditionalWeakTable<ScrollViewer, SmoothScrollController> _controllers = new();

        // ===== CACHED EASING FUNCTIONS (Avoid per-animation allocations) =====
        private static readonly BackEaseOut CachedBackEaseOut = new();
        private static readonly LinearEasing CachedLinearEasing = new();
        
        // ===== CACHED DURATIONS (Tuned for 240Hz+ responsiveness) =====
        private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(350);
        private static readonly TimeSpan OpacityDuration = TimeSpan.FromMilliseconds(280);
        private static readonly TimeSpan HoverScaleDuration = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan HoverRotateDuration = TimeSpan.FromMilliseconds(120);

        // ===== CACHED TRANSITION TEMPLATES (cloned per-control to avoid shared state issues) =====
        private static Transitions CreateEntranceTranslateTransitions() => new()
        {
            new DoubleTransition { Property = TranslateTransform.YProperty, Duration = EntranceDuration, Easing = CachedBackEaseOut }
        };

        private static Transitions CreateEntranceScaleTransitions() => new()
        {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = EntranceDuration, Easing = CachedBackEaseOut },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = EntranceDuration, Easing = CachedBackEaseOut }
        };

        private static Transitions CreateEntranceOpacityTransitions() => new()
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = OpacityDuration, Easing = CachedLinearEasing }
        };

        private static Transitions CreateHoverScaleTransitions() => new()
        {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = HoverScaleDuration },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = HoverScaleDuration }
        };

        private static Transitions CreateHoverRotateTransitions() => new()
        {
            new DoubleTransition { Property = Rotate3DTransform.AngleXProperty, Duration = HoverRotateDuration },
            new DoubleTransition { Property = Rotate3DTransform.AngleYProperty, Duration = HoverRotateDuration }
        };

        #region Entrance Animation

        public static readonly AttachedProperty<bool> EnableEntranceAnimationProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("EnableEntranceAnimation", typeof(AnimationHelper));



        public static void SetEnableEntranceAnimation(Control element, bool value) => element.SetValue(EnableEntranceAnimationProperty, value);

        public static readonly AttachedProperty<long> LastEntranceTimeProperty =
            AvaloniaProperty.RegisterAttached<Control, long>("LastEntranceTime", typeof(AnimationHelper));

        public static long GetLastEntranceTime(Control element) => element.GetValue(LastEntranceTimeProperty);
        public static void SetLastEntranceTime(Control element, long value) => element.SetValue(LastEntranceTimeProperty, value);

        static AnimationHelper()
        {
            EnableEntranceAnimationProperty.Changed.AddClassHandler<Control>(OnEnableEntranceAnimationChanged);
            EnableHoverAnimationProperty.Changed.AddClassHandler<Control>(OnEnableHoverAnimationChanged);
            EnableSmoothScrollingProperty.Changed.AddClassHandler<ScrollViewer>(OnEnableSmoothScrollingChanged);
        }

        private static long _lastStaggerTick;
        private static int _staggerCount;
        private const int MaxStagger = 12; // Reduced for faster batch appearance
        private const int StaggerDelayMs = 20; // Faster stagger for high-Hz

        private static void OnEnableEntranceAnimationChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                control.AttachedToVisualTree -= OnControlAttached;
                control.AttachedToVisualTree += OnControlAttached;
            }
            else
            {
                control.AttachedToVisualTree -= OnControlAttached;
            }
        }

        private static void OnControlAttached(object? sender, VisualTreeAttachmentEventArgs e) => RunEntranceAnimation(sender as Control);

        public static readonly AttachedProperty<System.Threading.CancellationTokenSource?> AnimationTokenProperty =
            AvaloniaProperty.RegisterAttached<Control, System.Threading.CancellationTokenSource?>("AnimationToken", typeof(AnimationHelper));

        public static System.Threading.CancellationTokenSource? GetAnimationToken(Control element) => element.GetValue(AnimationTokenProperty);
        public static void SetAnimationToken(Control element, System.Threading.CancellationTokenSource? value) => element.SetValue(AnimationTokenProperty, value);

        private static void RunEntranceAnimation(Control? control)
        {
            if (control == null) return;
            
            // DEBOUNCE
            var now = Stopwatch.GetTimestamp();
            var lastTime = GetLastEntranceTime(control);
            if ((now - lastTime) / (double)Stopwatch.Frequency < 0.2) return;
            SetLastEntranceTime(control, now);

            // Staggering Logic
            var dt = (now - _lastStaggerTick) / (double)Stopwatch.Frequency;
            
            if (dt > 0.15) _staggerCount = 0;
            _lastStaggerTick = now;
            var delayMs = Math.Min(_staggerCount * 15, 200); // Faster stagger
            _staggerCount++;

            // Ensure RenderTransform exists and is compatible
            if (control.RenderTransform is not TransformGroup)
            {
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform());
                group.Children.Add(new TranslateTransform());
                control.RenderTransform = group;
            }
            
            var groupTransform = control.RenderTransform as TransformGroup;
            var scaleTransform = groupTransform?.Children[0] as ScaleTransform;
            var translateTransform = groupTransform?.Children[1] as TranslateTransform;

            if (scaleTransform == null || translateTransform == null) return;

            // 1. DISABLE TRANSITIONS & RESET STATE
            // We must clear transitions to snap instantly to the start position without animating backward.
            control.Transitions = null;
            if (scaleTransform.Transitions != null) scaleTransform.Transitions = null;
            if (translateTransform.Transitions != null) translateTransform.Transitions = null;

            // Snap to Start (Hidden/Offset)
            control.Opacity = 0;
            translateTransform.Y = 20; 
            scaleTransform.ScaleX = 0.92;
            scaleTransform.ScaleY = 0.92;

            // 2. TRIGGER ANIMATION (NEXT FRAME)
            // Post to UI thread to allow the layout engine to process the "Snap" above.
            // Then re-enable transitions and set the target.
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Wait for Stagger
                if (delayMs > 0) await Task.Delay(delayMs);

                // Re-validate control is still attached to visual tree
                if (Avalonia.VisualTree.VisualExtensions.GetVisualRoot(control) == null) return;

                // Setup Transitions using cached factory methods (reduces allocations)
                translateTransform.Transitions = CreateEntranceTranslateTransitions();
                scaleTransform.Transitions = CreateEntranceScaleTransitions();
                control.Transitions = CreateEntranceOpacityTransitions();

                // Set Targets (Triggers the transition)
                control.Opacity = 1;
                translateTransform.Y = 0;
                scaleTransform.ScaleX = 1;
                scaleTransform.ScaleY = 1;

            }, DispatcherPriority.Render); // Use Render priority
        }

        #endregion

        public static readonly AttachedProperty<bool> EnableHoverAnimationProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("EnableHoverAnimation", typeof(AnimationHelper));

        public static bool GetEnableHoverAnimation(Control element) => element.GetValue(EnableHoverAnimationProperty);
        public static void SetEnableHoverAnimation(Control element, bool value) => element.SetValue(EnableHoverAnimationProperty, value);

        private static readonly ConditionalWeakTable<Control, TiltController> _tiltControllers = new();

        private static void OnEnableHoverAnimationChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                if (!_tiltControllers.TryGetValue(control, out var controller))
                {
                    controller = new TiltController(control);
                    _tiltControllers.Add(control, controller);
                }
                control.PointerEntered += controller.HandlePointerEntered;
                control.PointerMoved += controller.HandlePointerMoved;
                control.PointerExited += controller.HandlePointerExited;
            }
        }

        private class TiltController
        {
            private readonly Control _control;
            private double _targetAngleX, _targetAngleY;
            private double _currentAngleX, _currentAngleY;
            private double _targetScale = 1.0;
            private double _currentScale = 1.0;
            private double _glareOpacity;
            private double _targetGlareOpacity;
            private bool _isAnimating;
            private TimeSpan _lastTime;
            
            private const double SmoothSpeed = 20.0; // Higher = Snappier (tuned for 240Hz+)
            private const double MaxTilt = 12.0;

            public TiltController(Control control) => _control = control;

            public void HandlePointerEntered(object? sender, PointerEventArgs e)
            {
                _targetScale = 1.05;
                _targetGlareOpacity = 0.6;
                StartAnimation();
            }

            public void HandlePointerMoved(object? sender, PointerEventArgs e)
            {
                var p = e.GetPosition(_control);
                var nx = (p.X / _control.Bounds.Width) * 2.0 - 1.0;
                var ny = (p.Y / _control.Bounds.Height) * 2.0 - 1.0;
                
                _targetAngleY = -nx * MaxTilt;
                _targetAngleX = ny * MaxTilt;
                _targetGlareOpacity = 0.4 + (Math.Abs(nx) + Math.Abs(ny)) * 0.4;
                
                StartAnimation();
            }

            public void HandlePointerExited(object? sender, PointerEventArgs e)
            {
                _targetAngleX = 0;
                _targetAngleY = 0;
                _targetScale = 1.0;
                _targetGlareOpacity = 0;
            }

            private void StartAnimation()
            {
                if (_isAnimating) return;
                _isAnimating = true;
                _lastTime = TimeSpan.Zero;
                TopLevel.GetTopLevel(_control)?.RequestAnimationFrame(Animate);
            }

            private void Animate(TimeSpan time)
            {
                if (!_isAnimating) return;

                double dt = _lastTime == TimeSpan.Zero ? 0.004 : (time - _lastTime).TotalSeconds; // Default to 250Hz
                if (dt > 0.05) dt = 0.004; // Clamp large deltas (tab switch)
                _lastTime = time;

                // Exponential smoothing (Lerp)
                var factor = 1.0 - Math.Exp(-SmoothSpeed * dt);
                _currentAngleX += (_targetAngleX - _currentAngleX) * factor;
                _currentAngleY += (_targetAngleY - _currentAngleY) * factor;
                _currentScale += (_targetScale - _currentScale) * factor;
                _glareOpacity += (_targetGlareOpacity - _glareOpacity) * factor;

                ApplyTransforms();

                // Stop if we are close enough to targets
                bool atTarget = Math.Abs(_targetAngleX - _currentAngleX) < 0.01 && 
                               Math.Abs(_targetAngleY - _currentAngleY) < 0.01 &&
                               Math.Abs(_targetScale - _currentScale) < 0.001 &&
                               Math.Abs(_targetGlareOpacity - _glareOpacity) < 0.01;

                if (atTarget && _targetScale == 1.0)
                {
                    _isAnimating = false;
                    return;
                }

                TopLevel.GetTopLevel(_control)?.RequestAnimationFrame(Animate);
            }

            private void ApplyTransforms()
            {
                if (_control.RenderTransform is not TransformGroup group)
                {
                    group = new TransformGroup();
                    group.Children.Add(new ScaleTransform());
                    group.Children.Add(new Rotate3DTransform { Depth = 800 });
                    _control.RenderTransform = group;
                }

                var s = (ScaleTransform)group.Children[0];
                var r = (Rotate3DTransform)group.Children[1];

                s.ScaleX = _currentScale;
                s.ScaleY = _currentScale;
                
                r.CenterX = _control.Bounds.Width / 2;
                r.CenterY = _control.Bounds.Height / 2;
                r.AngleX = _currentAngleX;
                r.AngleY = _currentAngleY;

                // Dynamic Shadow Depth
                if (_control is Border b)
                {
                    var offX = -_currentAngleY * 0.8;
                    var offY = _currentAngleX * 0.8;
                    var blur = 15 + Math.Abs(_currentAngleX) + Math.Abs(_currentAngleY);
                    b.BoxShadow = new BoxShadows(new BoxShadow 
                    { 
                        OffsetX = offX, 
                        OffsetY = 15 + offY, 
                        Blur = blur, 
                        Color = Color.FromArgb(80, 0, 0, 0) 
                    });
                }

                // Enhanced Glare
                var glare = FindVisualChildByName(_control, "GlareLayer") as Border;
                if (glare != null)
                {
                    glare.Opacity = _glareOpacity;
                    if (glare.Background is LinearGradientBrush lgb)
                    {
                        var nx = -_currentAngleY / MaxTilt;
                        var ny = _currentAngleX / MaxTilt;
                        lgb.StartPoint = new RelativePoint(0.5 - nx, 0.5 - ny, RelativeUnit.Relative);
                        lgb.EndPoint = new RelativePoint(0.5 + nx, 0.5 + ny, RelativeUnit.Relative);
                    }
                }
            }

            private static Visual? FindVisualChildByName(Visual? parent, string name)
            {
                if (parent == null) return null;
                if (parent is Control c && c.Name == name) return parent;

                foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(parent))
                {
                    var found = FindVisualChildByName(child, name);
                    if (found != null) return found;
                }
                return null;
            }
        }


        #region Smooth Scrolling (High-Precision Fixed Step)

        public static readonly AttachedProperty<bool> EnableSmoothScrollingProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("EnableSmoothScrolling", typeof(AnimationHelper));

        public static bool GetEnableSmoothScrolling(ScrollViewer element) => element.GetValue(EnableSmoothScrollingProperty);
        public static void SetEnableSmoothScrolling(ScrollViewer element, bool value) => element.SetValue(EnableSmoothScrollingProperty, value);

        private static void OnEnableSmoothScrollingChanged(ScrollViewer scrollViewer, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                if (!_controllers.TryGetValue(scrollViewer, out var controller))
                {
                    controller = new SmoothScrollController(scrollViewer);
                    _controllers.Add(scrollViewer, controller);
                }
                scrollViewer.AddHandler(InputElement.PointerWheelChangedEvent, controller.HandleWheel, RoutingStrategies.Tunnel);
            }
        }

        private class SmoothScrollController
        {
            private readonly ScrollViewer _sv;
            private Avalonia.Vector _targetOffset;
            private Avalonia.Vector _currentOffset;
            private bool _isAnimating;
            private TimeSpan _lastTime;

            // Pre-calculated constants to avoid per-frame division
            private const double ScrollStep = 100.0;
            private const double InterpolationSpeed = 20.0; // Higher = snappier (tuned for 240Hz+)
            private const double SnapThreshold = 0.05; // Tighter snapping for precision
            private const double MaxDeltaSeconds = 0.05; // 20fps minimum (prevents lerp overshoot)

            public SmoothScrollController(ScrollViewer sv)
            {
                _sv = sv;
                _targetOffset = sv.Offset;
                _currentOffset = sv.Offset;
            }

            public void HandleWheel(object? sender, PointerWheelEventArgs e)
            {
                e.Handled = true;

                if (!_isAnimating)
                {
                    _targetOffset = _sv.Offset;
                    _currentOffset = _sv.Offset;
                    _isAnimating = true;
                    _lastTime = TimeSpan.Zero; // Will be set on first frame
                    TopLevel.GetTopLevel(_sv)?.RequestAnimationFrame(AnimateLoop);
                }

                // Calculate target with clamping
                double maxX = Math.Max(0, _sv.Extent.Width - _sv.Viewport.Width);
                double maxY = Math.Max(0, _sv.Extent.Height - _sv.Viewport.Height);
                
                _targetOffset = new Avalonia.Vector(
                    Math.Clamp(_targetOffset.X - e.Delta.X * ScrollStep, 0, maxX),
                    Math.Clamp(_targetOffset.Y - e.Delta.Y * ScrollStep, 0, maxY)
                );
            }

            private void AnimateLoop(TimeSpan time)
            {
                if (!_isAnimating) return;
                
                // Use provided TimeSpan directly - more accurate than Stopwatch for animation frames
                double dt;
                if (_lastTime == TimeSpan.Zero)
                {
                    dt = 0.004; // Assume ~250fps for first frame on high-Hz displays
                }
                else
                {
                    dt = (time - _lastTime).TotalSeconds;
                    if (dt > MaxDeltaSeconds) dt = 0.004; // Clamp large deltas
                    if (dt < 0.001) dt = 0.001; // Minimum delta to prevent division issues
                }
                _lastTime = time;

                var diff = _targetOffset - _currentOffset;

                // Check if we've reached the target
                if (diff.Length < SnapThreshold)
                {
                    _currentOffset = _targetOffset;
                    _sv.Offset = _targetOffset;
                    _isAnimating = false;
                    return;
                }

                // Exponential interpolation for smooth deceleration
                _currentOffset = _targetOffset - (diff * Math.Exp(-InterpolationSpeed * dt));
                _sv.Offset = _currentOffset;

                if (_isAnimating)
                    TopLevel.GetTopLevel(_sv)?.RequestAnimationFrame(AnimateLoop);
            }
        }

        #endregion
    }
}
