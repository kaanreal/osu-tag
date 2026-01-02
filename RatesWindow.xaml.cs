using System;
using System.Windows;
using System.Windows.Input;
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
            Loaded += (s, e) => PlayFadeInAnimation();
        }

        private void PlayFadeInAnimation()
        {
            // Fade-in animation
            var storyboard = new Storyboard();
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeIn);
            storyboard.Begin(this);
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
