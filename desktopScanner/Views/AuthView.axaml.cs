using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace desktopScanner.Views;

public partial class AuthView : Window
{
    public AuthView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}