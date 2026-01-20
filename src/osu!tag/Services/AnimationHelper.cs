using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia.Media;

namespace OsuTag.Services
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

        #region Hover Animation (Foreshortening Safe 3D)

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
                
                control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                
                // Initialize RenderTransform Group (Scale + Translate)
                // NO SKEW (Bulges)
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform());     // Index 0
                group.Children.Add(new TranslateTransform()); // Index 1
                control.RenderTransform = group;
            }
            else
            {
                control.PointerEntered -= OnPointerEntered;
                control.PointerMoved -= OnPointerMoved;
                control.PointerExited -= OnPointerExited;
            }
        }

        private static void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;
            control.Opacity = 1; // Ensure visible

            if (control.RenderTransform is TransformGroup group && group.Children[0] is ScaleTransform scale)
            {
                 // Hover ENTER Scale
                 scale.ScaleX = 1.05;
                 scale.ScaleY = 1.05;
            }
        }

        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;

            var bounds = control.Bounds;
            var point = e.GetPosition(control);
            
            // Normalize -1 to 1
            var x = (point.X / bounds.Width) * 2 - 1;
            var y = (point.Y / bounds.Height) * 2 - 1;

            if (control.RenderTransform is TransformGroup group)
            {
                var scale = group.Children[0] as ScaleTransform;
                var trans = group.Children[1] as TranslateTransform;

                if (scale != null && trans != null)
                {
                    // FORESHORTENING SIMULATION (Safe UI Thread)
                    // Reduce Scale as we move away from center to simulate depth
                    // "Turning away" -> smaller
                    
                    var dist = Math.Sqrt(x * x + y * y);
                    var factor = 0.03; // Max reduction
                    
                    // Maintain base hover scale of 1.05
                    var newScale = 1.05 - (dist * factor);
                    
                    scale.ScaleX = newScale;
                    scale.ScaleY = newScale;

                    // Counter-Movement (Parallax)
                    // Move slightly towards input to fake 3D pivot
                    trans.X = x * 2;
                    trans.Y = y * 2;
                }
            }
        }

        private static void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is not Control control) return;

            if (control.RenderTransform is TransformGroup group)
            {
                var scale = group.Children[0] as ScaleTransform;
                var trans = group.Children[1] as TranslateTransform;
                
                if (scale != null)
                {
                    scale.ScaleX = 1.0;
                    scale.ScaleY = 1.0;
                }
                if (trans != null)
                {
                    trans.X = 0;
                    trans.Y = 0;
                }
            }
        }

        #endregion
    }
}
