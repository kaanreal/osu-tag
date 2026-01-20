using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;

namespace Osutag.Services
{
    public static class AnimationHelper
    {
        #region Entrance Animation ("Falling" osu! style)

        public static readonly AttachedProperty<bool> EnableEntranceAnimationProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("EnableEntranceAnimation", typeof(AnimationHelper));

        public static bool GetEnableEntranceAnimation(Control element) => element.GetValue(EnableEntranceAnimationProperty);
        public static void SetEnableEntranceAnimation(Control element, bool value) => element.SetValue(EnableEntranceAnimationProperty, value);

        static AnimationHelper()
        {
            EnableEntranceAnimationProperty.Changed.AddClassHandler<Control>(OnEnableEntranceAnimationChanged);
            EnableHoverAnimationProperty.Changed.AddClassHandler<Control>(OnEnableHoverAnimationChanged);
        }

        private static void OnEnableEntranceAnimationChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.NewValue is true)
            {
                control.AttachedToVisualTree += OnAttachedToVisualTree;
                control.DataContextChanged += OnDataContextChanged;
            }
            else
            {
                control.AttachedToVisualTree -= OnAttachedToVisualTree;
                control.DataContextChanged -= OnDataContextChanged;
            }
        }

        private static void OnDataContextChanged(object? sender, EventArgs e)
        {
             if (sender is Control control) RunEntranceAnimation(control);
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not Control control) return;
            RunEntranceAnimation(control);
        }

        private static void RunEntranceAnimation(Control control)
        {
            var visual = ElementComposition.GetElementVisual(control);
            if (visual == null) return;
            
            var compositor = visual.Compositor;

            // FADE SAFEGUARD: Set initial visual state
            visual.Opacity = 0.01f; 
            visual.Offset = new Vector3(0, -60, 0); 
            visual.Scale = new Vector3(0.9f, 0.9f, 1f);

            var animationGroup = compositor.CreateAnimationGroup();
            
            // Opacity
            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.Target = "Opacity";
            opacityAnim.InsertKeyFrame(0f, 0f); 
            opacityAnim.InsertKeyFrame(0.5f, 1f); 
            opacityAnim.Duration = TimeSpan.FromMilliseconds(500);
            
            // Offset
            var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
            offsetAnim.Target = "Offset";
            offsetAnim.InsertKeyFrame(0f, new Vector3(0, -60, 0));
            offsetAnim.InsertKeyFrame(1f, Vector3.Zero);
            offsetAnim.Duration = TimeSpan.FromMilliseconds(500);
            
            // Scale
            var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
            scaleAnim.Target = "Scale";
            scaleAnim.InsertKeyFrame(0f, new Vector3(0.9f, 0.9f, 1f));
            scaleAnim.InsertKeyFrame(1f, Vector3.One); 
            scaleAnim.Duration = TimeSpan.FromMilliseconds(500);

            animationGroup.Add(opacityAnim);
            animationGroup.Add(offsetAnim);
            animationGroup.Add(scaleAnim);

            visual.StartAnimationGroup(animationGroup);
        }

        #endregion

        #region Hover Animation (Presenting & Smoothed)

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
                control.SizeChanged += OnControlSizeChanged;
                
                // Define Transform Group
                var group = new TransformGroup();
                
                // 1. Scale Transform (for "Presenting" Pop)
                var scale = new ScaleTransform();
                scale.Transitions = new Transitions
                {
                    new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new CubicEaseOut() },
                    new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new CubicEaseOut() }
                };
                group.Children.Add(scale);
                
                // 2. Rotate3D Transform (Perspective Tilt)
                var rot3D = new Rotate3DTransform 
                { 
                    Depth = 800,
                    CenterX = control.Bounds.Width / 2,
                    CenterY = control.Bounds.Height / 2
                };
                // Transitions provide the "Smoothness" / Physics feel automatically
                rot3D.Transitions = new Transitions
                {
                    new DoubleTransition { Property = Rotate3DTransform.AngleXProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new CubicEaseOut() },
                    new DoubleTransition { Property = Rotate3DTransform.AngleYProperty, Duration = TimeSpan.FromMilliseconds(200), Easing = new CubicEaseOut() }
                };
                group.Children.Add(rot3D);
                
                control.RenderTransform = group;
            }
            else
            {
                control.PointerEntered -= OnPointerEntered;
                control.PointerMoved -= OnPointerMoved;
                control.PointerExited -= OnPointerExited;
                control.SizeChanged -= OnControlSizeChanged;
            }
        }

        private static void OnControlSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (sender is Control control && control.RenderTransform is TransformGroup group 
                && group.Children.Count > 1 && group.Children[1] is Rotate3DTransform rot3D)
            {
                rot3D.CenterX = control.Bounds.Width / 2;
                rot3D.CenterY = control.Bounds.Height / 2;
            }
        }

        private static void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;
            
            if (control.RenderTransform is TransformGroup group && group.Children[0] is ScaleTransform scale)
            {
                 // "Presenting" Pop (Subtle 1.05x)
                 // Just setting the property triggers the smooth Transition defined above.
                 scale.ScaleX = 1.05;
                 scale.ScaleY = 1.05;
            }
        }

        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;
            var p = e.GetPosition(control);
            
            if (control.RenderTransform is TransformGroup group && group.Children.Count > 1 && group.Children[1] is Rotate3DTransform rot3D)
            {
                var w = control.Bounds.Width;
                var h = control.Bounds.Height;
                
                // Normalize -1 to 1
                var nx = (p.X / w) * 2.0 - 1.0;
                var ny = (p.Y / h) * 2.0 - 1.0;
                
                // Max Angle (Degrees) - Reduced intensity
                double maxAngle = 8.0;
                
                // Set Target Angle
                // The Transition (200ms CubicEaseOut) will smooth this value change automatically.
                rot3D.AngleY = -nx * maxAngle; // Yaw
                rot3D.AngleX = ny * maxAngle;  // Pitch
            }
        }

        private static void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;

             if (control.RenderTransform is TransformGroup group)
             {
                 var scale = group.Children[0] as ScaleTransform;
                 var rot3D = group.Children[1] as Rotate3DTransform;
                 
                 // Return to rest (Smoothly animated by Transitions)
                 if (scale != null) {
                    scale.ScaleX = 1.0;
                    scale.ScaleY = 1.0;
                 }
                 if (rot3D != null) {
                    rot3D.AngleX = 0.0;
                    rot3D.AngleY = 0.0;
                 }
             }
        }

        #endregion
    }
}
