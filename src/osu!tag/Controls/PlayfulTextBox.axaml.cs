using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Media.TextFormatting;

namespace Osutag.Controls
{
    public partial class PlayfulTextBox : UserControl
    {
        private TextBox? _inputBox;
        private Canvas? _textCanvas;
        private string _lastText = "";
        private List<VisualCharacter> _activeCharacters = new List<VisualCharacter>();
        private IDisposable? _textSubscription;

        // ===== CACHED INSTANCES (Avoid per-keystroke allocations) =====
        private static readonly Random SharedRandom = new();
        private static readonly BackEaseOut CachedBackEaseOut = new();
        private static readonly CubicEaseIn CachedCubicEaseIn = new();
        private static readonly TimeSpan PopInDuration = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan FallDuration = TimeSpan.FromMilliseconds(800);

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<PlayfulTextBox, string>(nameof(Text));

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<string> WatermarkProperty =
            AvaloniaProperty.Register<PlayfulTextBox, string>(nameof(Watermark));

        public string Watermark
        {
            get => GetValue(WatermarkProperty);
            set => SetValue(WatermarkProperty, value);
        }

        public new static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
            AvaloniaProperty.Register<PlayfulTextBox, HorizontalAlignment>(nameof(HorizontalContentAlignment), HorizontalAlignment.Left);

        public new HorizontalAlignment HorizontalContentAlignment
        {
            get => GetValue(HorizontalContentAlignmentProperty);
            set => SetValue(HorizontalContentAlignmentProperty, value);
        }

        public new static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty =
           AvaloniaProperty.Register<PlayfulTextBox, VerticalAlignment>(nameof(VerticalContentAlignment), VerticalAlignment.Center);

        public new VerticalAlignment VerticalContentAlignment
        {
            get => GetValue(VerticalContentAlignmentProperty);
            set => SetValue(VerticalContentAlignmentProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty)
            {
                UpdatePositions(Text);
            }
        }

        public PlayfulTextBox()
        {
            InitializeComponent();
            _inputBox = this.FindControl<TextBox>("InputBox");
            _textCanvas = this.FindControl<Canvas>("TextCanvas");

            if (_inputBox != null)
            {
                _textSubscription = _inputBox.GetObservable(TextBox.TextProperty).Subscribe(new SimpleObserver<string?>(OnTextChanged));

                this.GetObservable(TextProperty).Subscribe(new SimpleObserver<string?>(text =>
                {
                    if (_inputBox != null && _inputBox.Text != text)
                    {
                        _inputBox.Text = text;
                    }
                }));
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            // Initial position sync for Caret alignment
            Dispatcher.UIThread.InvokeAsync(() => UpdatePositions(""), DispatcherPriority.Render);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            // Sync on attach to ensure bounds are ready
            Dispatcher.UIThread.InvokeAsync(() => UpdatePositions(Text), DispatcherPriority.Render);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _textSubscription?.Dispose();
        }

        private void OnTextChanged(string? newText)
        {
            newText ??= "";
            if (newText == _lastText) return;

            // Update visible chars
            UpdateCharacters(newText);

            _lastText = newText;
            SetCurrentValue(TextProperty, newText);
        }

        private void UpdateCharacters(string newText)
        {
            // Robust reconciliation: Ensure _activeCharacters matches newText exactly in order.

            // 1. Remove characters from _activeCharacters that don't match the new text at their position
            // OR if they are beyond the new text length.
            for (int i = _activeCharacters.Count - 1; i >= 0; i--)
            {
                bool shouldRemove = false;
                if (i >= newText.Length)
                {
                    shouldRemove = true;
                }
                else if (_activeCharacters[i].Char != newText[i])
                {
                    // This position changed (e.g. paste over)
                    shouldRemove = true;
                }

                if (shouldRemove)
                {
                    var charToRemove = _activeCharacters[i];
                    _activeCharacters.RemoveAt(i);
                    AnimateFall(charToRemove);
                }
            }

            // 2. Insert missing characters
            for (int i = 0; i < newText.Length; i++)
            {
                // If we have a character at this index, it must be correct due to step 1.
                if (i < _activeCharacters.Count)
                {
                    // Already correct, skip
                    continue;
                }

                // Need to add this character
                char c = newText[i];
                var visualChar = new VisualCharacter
                {
                    Char = c,
                    Control = CreateCharacterControl(c)
                };

                _activeCharacters.Add(visualChar);
                _textCanvas?.Children.Add(visualChar.Control);
                AnimatePopIn(visualChar);
            }

            // 3. Update Positions for all active characters
            UpdatePositions(newText);
        }

        private TextBlock CreateCharacterControl(char c)
        {
            return new TextBlock
            {
                Text = c.ToString(),
                FontSize = this.FontSize > 0 ? this.FontSize : 16,
                Foreground = this.Foreground ?? Brushes.White,

                RenderTransform = new TransformGroup
                {
                    Children = new Transforms
                    {
                        new ScaleTransform(),
                        new TranslateTransform(),
                        new RotateTransform()
                    }
                },
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };
        }

        private void UpdatePositions(string text)
        {
            // We need to measure where each character SHOULD be.
            if (_inputBox == null) return;

            text ??= "";
            var typeface = new Typeface(_inputBox.FontFamily, _inputBox.FontStyle, _inputBox.FontWeight, _inputBox.FontStretch);

            // Single TextLayout for both width calculation and hit testing (was creating 2)
            var textLayout = new TextLayout(text, typeface, this.FontSize, Brushes.Black);
            double totalWidth = textLayout.Width;
            double startOffset = 0;

            if (HorizontalContentAlignment == HorizontalAlignment.Center)
            {
                startOffset = (_inputBox.Bounds.Width - totalWidth) / 2;
                if (startOffset < 0) startOffset = 0; // Left align if overflow
            }

            for (int i = 0; i < _activeCharacters.Count; i++)
            {
                if (i >= text.Length) break;

                // Get position of character i
                var hitTest = textLayout.HitTestTextPosition(i);

                // Add Padding logic to match underlying TextBox
                double x = startOffset + hitTest.X + _inputBox.Padding.Left;

                // Calculate vertical centering using the SAME font properties as the InputBox
                // to ensure the hidden text and visual text share identical baselines.
                double verticalOffset = (_inputBox.Bounds.Height - textLayout.Height) / 2;
                if (double.IsNaN(verticalOffset) || verticalOffset < 0) verticalOffset = 0;

                // CRITICAL SYNC: Match the InputBox's internal text alignment
                // We align the invisible text by pushing it down with padding.
                if (Math.Abs(_inputBox.Padding.Top - verticalOffset) > 0.01)
                {
                    _inputBox.Padding = new Thickness(_inputBox.Padding.Left, verticalOffset, _inputBox.Padding.Right, _inputBox.Padding.Bottom);
                }

                if (i < _activeCharacters.Count)
                {
                    // Apply Position (Use verticalOffset directly as we are top-aligned relative to container, 
                    // and TextBox is now padded down to match)
                    Canvas.SetLeft(_activeCharacters[i].Control, x);
                    Canvas.SetTop(_activeCharacters[i].Control, verticalOffset);
                }
            }

            // Handle empty text case to ensure Caret is still centered
            if (text.Length == 0)
            {
                var textLayoutEmpty = new TextLayout(" ", typeface, this.FontSize, Brushes.Black);
                double verticalOffset = (_inputBox.Bounds.Height - textLayoutEmpty.Height) / 2;
                if (double.IsNaN(verticalOffset) || verticalOffset < 0) verticalOffset = 0;

                if (Math.Abs(_inputBox.Padding.Top - verticalOffset) > 0.01)
                {
                    _inputBox.Padding = new Thickness(_inputBox.Padding.Left, verticalOffset, _inputBox.Padding.Right, _inputBox.Padding.Bottom);
                }
            }
        }

        private void AnimatePopIn(VisualCharacter vc)
        {
            if (!(vc.Control.RenderTransform is TransformGroup tg) || tg.Children.Count < 3) return;
            var scaleTransform = tg.Children[0] as ScaleTransform;
            var rotateTransform = tg.Children[2] as RotateTransform;

            if (scaleTransform == null || rotateTransform == null) return;

            // Random slight rotation for playfulness (using shared Random to avoid allocations)
            double startAngle = SharedRandom.Next(-5, 5);

            var animation = new Animation
            {
                Duration = PopInDuration,
                Easing = CachedBackEaseOut, // Cached easing instance
                Children =
                 {
                     new KeyFrame
                     {
                         Cue = new Cue(0),
                         Setters =
                         {
                             new Setter(ScaleTransform.ScaleXProperty, 0.0),
                             new Setter(ScaleTransform.ScaleYProperty, 0.0),
                             new Setter(RotateTransform.AngleProperty, startAngle)
                         }
                     },
                     new KeyFrame
                     {
                         Cue = new Cue(1),
                         Setters =
                         {
                             new Setter(ScaleTransform.ScaleXProperty, 1.0),
                             new Setter(ScaleTransform.ScaleYProperty, 1.0),
                             new Setter(RotateTransform.AngleProperty, 0.0)
                         }
                     }
                 }
            };
            animation.RunAsync(vc.Control);
        }

        private void AnimateFall(VisualCharacter vc)
        {
            // Detach from main list logic, but keep in Canvas until animation done
            var control = vc.Control;

            // Bring to front
            control.ZIndex = 1000;

            // Randomize fall direction slightly (using shared Random to avoid allocations)
            double rotateTo = SharedRandom.Next(-180, 180); // More dramatic rotation
            double moveX = SharedRandom.Next(-100, 100);    // Scatter more

            var animation = new Animation
            {
                Duration = FallDuration, // Cached duration
                Easing = CachedCubicEaseIn, // Cached easing instance
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(TranslateTransform.YProperty, 800.0), // Fall way down (off screen)
                            new Setter(TranslateTransform.XProperty, moveX),
                            new Setter(RotateTransform.AngleProperty, rotateTo),
                            new Setter(Visual.OpacityProperty, 0.0) // Fade out at end
                        }
                    }
                }
            };

            animation.RunAsync(control).ContinueWith(_ =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _textCanvas?.Children.Remove(control);
                });
            });
        }

        private class VisualCharacter
        {
            public char Char { get; set; }
            public TextBlock Control { get; set; } = null!;
        }

        private class SimpleObserver<T> : IObserver<T>
        {
            private readonly Action<T> _onNext;
            public SimpleObserver(Action<T> onNext) => _onNext = onNext;
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(T value) => _onNext(value);
        }
    }
}
