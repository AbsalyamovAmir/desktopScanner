using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace desktopScanner.Views;

public partial class AuthView : UserControl  // Изменено с Window на UserControl
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