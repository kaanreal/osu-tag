using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
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

namespace OsuTag.Controls
{
    public partial class PlayfulTextBox : UserControl
    {
        private TextBox? _inputBox;
        private Canvas? _textCanvas;
        private string _lastText = "";
        private List<VisualCharacter> _activeCharacters = new List<VisualCharacter>();
        private IDisposable? _textSubscription;

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
            // Simple Diffing
            // If newText is longer, we typed something
            // If shorter, we deleted something.
            
            // This naive implementation rebuilds/moves characters based on index.
            // For a robust implementation, we would try to track identity, but index is okay for simple typing.
            
            // 1. Handle Deletions (Falling Animation)
            if (newText.Length < _activeCharacters.Count)
            {
                // Find where the change happened
                int diffIndex = 0;
                while (diffIndex < newText.Length && newText[diffIndex] == _activeCharacters[diffIndex].Char)
                {
                    diffIndex++;
                }

                // Any characters after this point might have shifted, but the one at diffIndex is definitely gone/changed
                // In a multi-character delete, we might lose multiple.
                int countToRemove = _activeCharacters.Count - newText.Length;
                
                for (int i = 0; i < countToRemove; i++)
                {
                    if (diffIndex < _activeCharacters.Count)
                    {
                        var charToRemove = _activeCharacters[diffIndex];
                        _activeCharacters.RemoveAt(diffIndex);
                        AnimateFall(charToRemove);
                    }
                }
            }
            // 2. Handle Additions (Pop-in Animation)
            else if (newText.Length > _activeCharacters.Count)
            {
                 int diffIndex = 0;
                while (diffIndex < _lastText.Length && diffIndex < newText.Length && newText[diffIndex] == _lastText[diffIndex])
                {
                    diffIndex++;
                }

                // Insert new characters
                int countToAdd = newText.Length - _activeCharacters.Count;
                for (int i = 0; i < countToAdd; i++)
                {
                    int index = diffIndex + i;
                    char c = newText[index];
                    
                    var visualChar = new VisualCharacter
                    {
                        Char = c,
                        Control = CreateCharacterControl(c)
                    };

                    if (index >= _activeCharacters.Count)
                        _activeCharacters.Add(visualChar);
                    else
                        _activeCharacters.Insert(index, visualChar);

                    _textCanvas?.Children.Add(visualChar.Control);
                    AnimatePopIn(visualChar);
                }
            }
            // 3. Handle same length (Replace) - rare in simple typing, usually pasteOver
            else
            {
                // Just update chars
                for (int i = 0; i < newText.Length; i++)
                {
                    if (_activeCharacters[i].Char != newText[i])
                    {
                         var oldChar = _activeCharacters[i];
                         AnimateFall(oldChar);
                         
                         var newCharControl = CreateCharacterControl(newText[i]);
                         var newVisual = new VisualCharacter { Char = newText[i], Control = newCharControl };
                          _activeCharacters[i] = newVisual;
                          _textCanvas?.Children.Add(newCharControl);
                          AnimatePopIn(newVisual);
                    }
                }
            }

            // 4. Update Positions for all active characters
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
             
             if (string.IsNullOrEmpty(text) || _inputBox == null) return;
             
             var typeface = new Typeface(_inputBox.FontFamily, _inputBox.FontStyle, _inputBox.FontWeight, _inputBox.FontStretch);

             // Center alignment adjustment?
             // If Box is centered, we need total width.
             var fullLayout = new TextLayout(text, typeface, this.FontSize, Brushes.Black);
             double totalWidth = fullLayout.Width;
             double startOffset = 0;

             if (HorizontalContentAlignment == HorizontalAlignment.Center)
             {
                 startOffset = (_inputBox.Bounds.Width - totalWidth) / 2;
                 if (startOffset < 0) startOffset = 0; // Left align if overflow
             }
             
             // TextLayout gives us precise positions
             var textLayout = new TextLayout(text, typeface, this.FontSize, Brushes.Black);
             
             for (int i = 0; i < _activeCharacters.Count; i++)
             {
                 if (i >= text.Length) break;
                 
                 // Get position of character i
                 var hitTest = textLayout.HitTestTextPosition(i);
                 
                 // Add Padding logic to match underlying TextBox
                 double x = startOffset + hitTest.X + _inputBox.Padding.Left;
                 
                 // Center vertically in the box
                 double verticalOffset = (_inputBox.Bounds.Height - this.FontSize * 1.5) / 2;
                 
                 // Apply Position
                 Canvas.SetLeft(_activeCharacters[i].Control, x);
                 Canvas.SetTop(_activeCharacters[i].Control, verticalOffset);
             }
        }

        private void AnimatePopIn(VisualCharacter vc)
        {
             if (!(vc.Control.RenderTransform is TransformGroup tg) || tg.Children.Count < 3) return;
             var scaleTransform = tg.Children[0] as ScaleTransform;
             var rotateTransform = tg.Children[2] as RotateTransform;

             if (scaleTransform == null || rotateTransform == null) return;
             
             // Random slight rotation for playfulness (reduced for cleanliness)
             var rnd = new Random();
             double startAngle = rnd.Next(-5, 5);

             var animation = new Animation
             {
                 Duration = TimeSpan.FromMilliseconds(300),
                 Easing = new BackEaseOut(), // Cleaner pop
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

            // Randomize fall direction slightly
            var rnd = new Random();
            double rotateTo = rnd.Next(-180, 180); // More dramatic rotation
            double moveX = rnd.Next(-100, 100);    // Scatter more
            
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(800), // Longer fall time
                Easing = new CubicEaseIn(), // Accelerate (Gravity)
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
