using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OsuTag.Views
{
    public partial class MessageWindow : Window
    {
        public MessageWindow()
        {
            InitializeComponent();
        }

        public MessageWindow(string title, string message) : this()
        {
            var titleBlock = this.FindControl<TextBlock>("TitleText");
            if (titleBlock != null) titleBlock.Text = title;

            var msgBlock = this.FindControl<TextBlock>("MessageText");
            if (msgBlock != null) msgBlock.Text = message;
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
