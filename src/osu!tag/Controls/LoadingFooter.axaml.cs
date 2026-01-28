using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Osutag.Controls
{
    public partial class LoadingFooter : UserControl
    {
        public LoadingFooter()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
