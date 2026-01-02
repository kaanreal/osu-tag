using System;
using System.Windows;

namespace OsuTag
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = (Exception)args.ExceptionObject;
                MessageBox.Show($"An unexpected error occurred:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}
