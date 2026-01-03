using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OsuTag.ViewModels;

namespace OsuTag
{
    public partial class RatesWindow : Window
    {
        public RatesWindow(MapItemGroup mapGroup)
        {
            InitializeComponent();
            DataContext = mapGroup;
            Owner = null;
            Loaded += (s, e) => PlayOpenAnimation();
        }

        private void PlayOpenAnimation()
        {
            // Apply transform to root border, not window
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);
            RootBorder.Opacity = 0;
            
            var storyboard = new Storyboard();
            
            // Fade in
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, RootBorder);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeIn);
            
            // Scale in
            var scaleX = new DoubleAnimation
            {
                From = 0.95,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleX, RootBorder);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleX);
            
            var scaleY = new DoubleAnimation
            {
                From = 0.95,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleY, RootBorder);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleY);
            
            storyboard.Begin();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Difficulty_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is DifficultyItem diffItem)
            {
                diffItem.IsSelected = !diffItem.IsSelected;
                e.Handled = true;
            }
        }
    }
}
