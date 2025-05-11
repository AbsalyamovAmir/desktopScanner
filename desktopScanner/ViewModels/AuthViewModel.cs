using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using desktopScanner.Views;
using ReactiveUI;

namespace desktopScanner.ViewModels;

public class AuthViewModel : ReactiveObject
{
    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ReactiveCommand<Unit, Unit> LoginCommand { get; }

    public AuthViewModel()
    {
        LoginCommand = ReactiveCommand.CreateFromTask(Login);
    }

    private async Task Login()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsJsonAsync("https://disribprotect.ru/agent-login", new
            {
                username = Username,
                password = Password
            });

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Неверные учетные данные";
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            var accessToken = json.RootElement.GetProperty("access_token").GetString();

            if (string.IsNullOrEmpty(accessToken))
            {
                ErrorMessage = "Ошибка при получении токена";
                return;
            }

            // Сохраняем токен и переходим к главному окну
            App.AccessToken = accessToken;
            ShowMainWindow();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }
    }
}