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
        
        // ===== CACHED DURATIONS =====
        private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan OpacityDuration = TimeSpan.FromMilliseconds(400);
        private static readonly TimeSpan HoverScaleDuration = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan HoverRotateDuration = TimeSpan.FromMilliseconds(150);

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
        private const int MaxStagger = 15;
        private const int StaggerDelayMs = 30;

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
            var delayMs = Math.Min(_staggerCount * 20, 300); 
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

        #region Hover Animation

        public static readonly AttachedProperty<bool> EnableHoverAnimationProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("EnableHoverAnimation", typeof(AnimationHelper));

        public static bool GetEnableHoverAnimation(Control element) => element.GetValue(EnableHoverAnimationProperty);
        public static void SetEnableHoverAnimation(Control element, bool value) => element.SetValue(EnableHoverAnimationProperty, value);

        private static void OnEnableHoverAnimationChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                control.PointerEntered += OnPointerEntered;
                control.PointerMoved += OnPointerMoved;
                control.PointerExited += OnPointerExited;
            }
        }

        private static void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control)
            {
                if (control.RenderTransform is not TransformGroup)
                {
                    var group = new TransformGroup();
                    var scale = new ScaleTransform();
                    scale.Transitions = CreateHoverScaleTransitions();
                    group.Children.Add(scale);
                    var rot3D = new Rotate3DTransform { Depth = 800 };
                    rot3D.Transitions = CreateHoverRotateTransitions();
                    group.Children.Add(rot3D);
                    control.RenderTransform = group;
                }
                
                if (control.RenderTransform is TransformGroup g && g.Children[0] is ScaleTransform s)
                {
                    s.ScaleX = 1.05;
                    s.ScaleY = 1.05;
                }
            }
        }

        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.RenderTransform is TransformGroup group && group.Children.Count > 1 && group.Children[1] is Rotate3DTransform rot3D)
            {
                var p = e.GetPosition(control);
                rot3D.CenterX = control.Bounds.Width / 2;
                rot3D.CenterY = control.Bounds.Height / 2;
                var nx = (p.X / control.Bounds.Width) * 2.0 - 1.0;
                var ny = (p.Y / control.Bounds.Height) * 2.0 - 1.0;
                rot3D.AngleY = -nx * 6.0;
                rot3D.AngleX = ny * 6.0;
            }
        }

        private static void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.RenderTransform is TransformGroup group)
            {
                if (group.Children[0] is ScaleTransform s) { s.ScaleX = 1.0; s.ScaleY = 1.0; }
                if (group.Children.Count > 1 && group.Children[1] is Rotate3DTransform r) { r.AngleX = 0; r.AngleY = 0; }
            }
        }

        #endregion

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
            private const double InterpolationSpeed = 12.0;
            private const double SnapThreshold = 0.1;
            private const double MaxDeltaSeconds = 0.1;

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
                    dt = 0.016; // Assume ~60fps for first frame
                }
                else
                {
                    dt = (time - _lastTime).TotalSeconds;
                    if (dt > MaxDeltaSeconds) dt = 0.016; // Clamp large deltas (tab switch, etc.)
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
