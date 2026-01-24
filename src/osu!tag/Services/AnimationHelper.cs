using System;
using System.Runtime.CompilerServices;
using System.Diagnostics;
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

namespace Osutag.Services
{
    public static class AnimationHelper
    {
        private static readonly ConditionalWeakTable<ScrollViewer, SmoothScrollController> _controllers = new();

        #region Entrance Animation

        public static readonly AttachedProperty<bool> EnableEntranceAnimationProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("EnableEntranceAnimation", typeof(AnimationHelper));

        public static bool GetEnableEntranceAnimation(Control element) => element.GetValue(EnableEntranceAnimationProperty);
        public static void SetEnableEntranceAnimation(Control element, bool value) => element.SetValue(EnableEntranceAnimationProperty, value);

        static AnimationHelper()
        {
            EnableEntranceAnimationProperty.Changed.AddClassHandler<Control>(OnEnableEntranceAnimationChanged);
            EnableHoverAnimationProperty.Changed.AddClassHandler<Control>(OnEnableHoverAnimationChanged);
            EnableSmoothScrollingProperty.Changed.AddClassHandler<ScrollViewer>(OnEnableSmoothScrollingChanged);
        }

        private static void OnEnableEntranceAnimationChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                control.AttachedToVisualTree -= OnControlAttached;
                control.AttachedToVisualTree += OnControlAttached;
            }
        }

        private static void OnControlAttached(object? sender, VisualTreeAttachmentEventArgs e) => RunEntranceAnimation(sender as Control);

        private static void RunEntranceAnimation(Control? control)
        {
            if (control == null) return;
            var visual = ElementComposition.GetElementVisual(control);
            if (visual == null) return;
            
            var compositor = visual.Compositor;
            visual.Opacity = 0.01f; 
            visual.Offset = new System.Numerics.Vector3(0, -40, 0); 

            var animationGroup = compositor.CreateAnimationGroup();
            
            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.Target = "Opacity";
            opacityAnim.InsertKeyFrame(0f, 0f); 
            opacityAnim.InsertKeyFrame(0.5f, 1f); 
            opacityAnim.Duration = TimeSpan.FromMilliseconds(400);
            
            var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
            offsetAnim.Target = "Offset";
            offsetAnim.InsertKeyFrame(0f, new System.Numerics.Vector3(0, -40, 0));
            offsetAnim.InsertKeyFrame(1f, System.Numerics.Vector3.Zero);
            offsetAnim.Duration = TimeSpan.FromMilliseconds(400);
            
            animationGroup.Add(opacityAnim);
            animationGroup.Add(offsetAnim);
            visual.StartAnimationGroup(animationGroup);
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
                    scale.Transitions = new Transitions { new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromMilliseconds(200) }, new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromMilliseconds(200) } };
                    group.Children.Add(scale);
                    var rot3D = new Rotate3DTransform { Depth = 800 };
                    rot3D.Transitions = new Transitions { new DoubleTransition { Property = Rotate3DTransform.AngleXProperty, Duration = TimeSpan.FromMilliseconds(150) }, new DoubleTransition { Property = Rotate3DTransform.AngleYProperty, Duration = TimeSpan.FromMilliseconds(150) } };
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
            private long _lastTimestamp;

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
                    _lastTimestamp = Stopwatch.GetTimestamp();
                    TopLevel.GetTopLevel(_sv)?.RequestAnimationFrame(AnimateLoop);
                }

                const double step = 100.0;
                _targetOffset = new Avalonia.Vector(
                    Math.Max(0, Math.Min(_targetOffset.X - e.Delta.X * step, _sv.Extent.Width - _sv.Viewport.Width)),
                    Math.Max(0, Math.Min(_targetOffset.Y - e.Delta.Y * step, _sv.Extent.Height - _sv.Viewport.Height))
                );
            }

            private void AnimateLoop(TimeSpan time)
            {
                if (!_isAnimating) return;
                
                long now = Stopwatch.GetTimestamp();
                double dt = (double)(now - _lastTimestamp) / Stopwatch.Frequency;
                _lastTimestamp = now;

                if (dt > 0.1) dt = 0.016; 

                var diff = _targetOffset - _currentOffset;

                if (diff.Length < 0.1)
                {
                    _currentOffset = _targetOffset;
                    _sv.Offset = _targetOffset;
                    _isAnimating = false;
                    return;
                }

                const double speed = 12.0; 
                _currentOffset = _targetOffset - (diff * Math.Exp(-speed * dt));
                _sv.Offset = _currentOffset;

                if (_isAnimating)
                    TopLevel.GetTopLevel(_sv)?.RequestAnimationFrame(AnimateLoop);
            }
        }

        #endregion
    }
}
