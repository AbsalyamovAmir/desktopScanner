using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace desktopScanner.Views;

public partial class MainContentView : UserControl
{
    public MainContentView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}