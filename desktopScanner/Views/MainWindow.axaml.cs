using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using desktopScanner.ViewModels;

namespace desktopScanner.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var authView = new AuthView
        {
            DataContext = new AuthViewModel(this)
        };
        this.Content = authView;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}