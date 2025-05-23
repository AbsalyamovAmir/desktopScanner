using System;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using desktopScanner.Views;
using ReactiveUI;

namespace desktopScanner.ViewModels;

public class AuthViewModel : ReactiveObject
{
    private string _email = string.Empty;

    public string Email
    {
        get => _email;
        set => this.RaiseAndSetIfChanged(ref _email, value);
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

    private readonly Window _mainWindow;

    public AuthViewModel(Window mainWindow)
    {
        _mainWindow = mainWindow;
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
                email = Email,
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

            // Сохраняем токен
            App.AccessToken = accessToken;

            // Отправляем данные о подключении
            await SendConnectionData(accessToken);

            // Переключаемся на главный экран
            ShowMainContent();
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

    private async Task SendConnectionData(string accessToken)
    {
        try
        {
            var connectionData = new
            {
                hostname = Environment.MachineName,
                ip = GetLocalIPAddress(),
                os = Environment.OSVersion.ToString(),
                uptime = GetSystemUptime(),
                cpu = GetCpuInfo(),
                ram = GetTotalMemory(),
                access_token = accessToken
            };

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsJsonAsync("https://disribprotect.ru/conection", connectionData);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Не удалось отправить данные о подключении");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при отправке данных о подключении: {ex.Message}");
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            return "Не определен";
        }
        catch
        {
            return "Не определен";
        }
    }

    private string GetSystemUptime()
    {
        try
        {
            using (var uptime = new PerformanceCounter("System", "System Up Time"))
            {
                uptime.NextValue();
                TimeSpan uptimeSpan = TimeSpan.FromSeconds(uptime.NextValue());
                return $"{uptimeSpan.Days}d {uptimeSpan.Hours}h {uptimeSpan.Minutes}m";
            }
        }
        catch
        {
            return "Не определен";
        }
    }

    private string GetCpuInfo()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
            {
                foreach (var o in searcher.Get())
                {
                    var obj = (ManagementObject)o;
                    return obj["Name"].ToString();
                }
            }

            return "Не определен";
        }
        catch
        {
            return "Не определен";
        }
    }

    private string GetTotalMemory()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                    var totalGB = totalBytes / (1024 * 1024 * 1024);
                    return $"{totalGB} GB";
                }
            }

            return "Не определен";
        }
        catch
        {
            return "Не определен";
        }
    }

    private void ShowMainContent()
    {
        // Создаем и устанавливаем главное содержимое окна
        var mainContent = new MainContentView
        {
            DataContext = new MainWindowViewModel()
        };

        _mainWindow.Content = mainContent;
        _mainWindow.Title = "Desktop Scanner - Main";
    }
}