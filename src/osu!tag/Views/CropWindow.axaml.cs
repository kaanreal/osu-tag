using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Shapes;
using System;

namespace Osutag.Views
{
    public partial class CropWindow : Window
    {
        private Point _lastMousePos;
        private bool _isDragging;
        private Bitmap? _bitmap;
        private Rect _holeRect;
        
        private ScaleTransform? _imageScale;
        private TranslateTransform? _imageTranslate;
        private RectangleGeometry? _holeGeometry;

        // Result properties
        public bool IsConfirmed { get; private set; }
        public int CropX { get; private set; }
        public int CropY { get; private set; }
        public int CropSize { get; private set; }

        public CropWindow()
        {
            InitializeComponent();
        }

        public CropWindow(string imagePath) : this()
        {
            if (System.IO.File.Exists(imagePath))
            {
                _bitmap = new Bitmap(imagePath);
                var img = this.FindControl<Image>("TargetImage");
                if (img != null) img.Source = _bitmap;
            }

            this.Opened += CropWindow_Opened;
            
            var canvas = this.FindControl<Canvas>("ImageCanvas");
            if (canvas != null)
            {
                canvas.PointerPressed += ImageCanvas_PointerPressed;
                canvas.PointerMoved += ImageCanvas_PointerMoved;
                canvas.PointerReleased += ImageCanvas_PointerReleased;
                canvas.PointerWheelChanged += ImageCanvas_PointerWheelChanged;
            }

            var slider = this.FindControl<Slider>("ZoomSlider");
            if (slider != null)
                slider.PropertyChanged += ZoomSlider_PropertyChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void CropWindow_Opened(object? sender, EventArgs e)
        {
            // Initialize references
            var img = this.FindControl<Image>("TargetImage");
            if (img != null && img.RenderTransform is TransformGroup group)
            {
                _imageScale = group.Children[0] as ScaleTransform;
                _imageTranslate = group.Children[1] as TranslateTransform;
            }

            var pathControl = this.FindControl<Avalonia.Controls.Shapes.Path>("HolePath");
            if (pathControl != null && pathControl.Data is CombinedGeometry combined && combined.Geometry2 is RectangleGeometry rectGeo)
            {
                _holeGeometry = rectGeo;
            }

            // Center the Hole
            var canvas = this.FindControl<Canvas>("ImageCanvas");
            if (canvas == null || _holeGeometry == null) return;

            double width = canvas.Bounds.Width;
            double height = canvas.Bounds.Height;
            double side = 400;

            _holeRect = new Rect((width - side) / 2, (height - side) / 2, side, side);
            _holeGeometry.Rect = _holeRect;

            // Center Image initially
            if (_bitmap != null && _imageScale != null && _imageTranslate != null)
            {
                var scale = Math.Max(side / _bitmap.Size.Width, side / _bitmap.Size.Height);
                _imageScale.ScaleX = scale;
                _imageScale.ScaleY = scale;
                
                var slider = this.FindControl<Slider>("ZoomSlider");
                if (slider != null) slider.Value = scale;

                _imageTranslate.X = (width - (_bitmap.Size.Width * scale)) / 2;
                _imageTranslate.Y = (height - (_bitmap.Size.Height * scale)) / 2;
            }
        }

        private void ImageCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(this);
        }

        private void ImageCanvas_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging || _imageTranslate == null) return;

            var pos = e.GetPosition(this);
            var delta = pos - _lastMousePos;
            _lastMousePos = pos;

            _imageTranslate.X += delta.X;
            _imageTranslate.Y += delta.Y;
        }

        private void ImageCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
        }

        private void ImageCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_imageScale == null) return;
            double zoomFactor = 1.1;
            double newScale = e.Delta.Y > 0 ? _imageScale.ScaleX * zoomFactor : _imageScale.ScaleX / zoomFactor;
            var canvas = this.FindControl<Canvas>("ImageCanvas");
            if (canvas != null)
                ApplyZoom(newScale, e.GetPosition(canvas));
        }

        private void ZoomSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Value" && sender is Slider slider && _imageScale != null)
            {
                if (Math.Abs(slider.Value - _imageScale.ScaleX) > 0.01)
                {
                    var canvas = this.FindControl<Canvas>("ImageCanvas");
                    if (canvas != null)
                        ApplyZoom(slider.Value, new Point(canvas.Bounds.Width/2, canvas.Bounds.Height/2));
                }
            }
        }

        private void ApplyZoom(double newScale, Point center)
        {
            if (_imageScale == null || _imageTranslate == null) return;

            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 10) newScale = 10;

            double oldScale = _imageScale.ScaleX;
            double factor = newScale / oldScale;

            double vX = center.X - _imageTranslate.X;
            double vY = center.Y - _imageTranslate.Y;

            _imageTranslate.X = center.X - vX * factor;
            _imageTranslate.Y = center.Y - vY * factor;

            _imageScale.ScaleX = newScale;
            _imageScale.ScaleY = newScale;
            
            var slider = this.FindControl<Slider>("ZoomSlider");
            if (slider != null && Math.Abs(slider.Value - newScale) > 0.01)
            {
                slider.Value = newScale;
            }
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            if (_bitmap == null || _imageScale == null || _imageTranslate == null) return;

            double scale = _imageScale.ScaleX;
            double cropX = (_holeRect.X - _imageTranslate.X) / scale;
            double cropY = (_holeRect.Y - _imageTranslate.Y) / scale;
            double cropSize = _holeRect.Width / scale;

            CropX = (int)Math.Round(cropX);
            CropY = (int)Math.Round(cropY);
            CropSize = (int)Math.Round(cropSize);
            IsConfirmed = true;
            Close();
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }
}
