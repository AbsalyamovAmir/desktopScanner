using System;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Controls;
using desktopScanner.Services;
using ReactiveUI;

namespace desktopScanner.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
       private readonly HttpClient _httpClient = new HttpClient();
    private string _report = string.Empty;
    private IDisposable _timerSubscription;
    
    public string Report
    {
        get => _report;
        set => this.RaiseAndSetIfChanged(ref _report, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    public ReactiveCommand<Unit, Unit> ScanCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public MainWindowViewModel()
    {
        ScanCommand = ReactiveCommand.CreateFromTask(ScanSoftware);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveReport);
        
        // Запускаем таймер при создании ViewModel
        StartAutoScanTimer();
    }

    private void StartAutoScanTimer()
    {
        // Таймер будет срабатывать сразу при старте, затем каждые 60 минут
        _timerSubscription = Observable.Timer(
            TimeSpan.Zero, 
            TimeSpan.FromHours(1))
            .Subscribe(async _ => await ScanSoftware());
    }

    private async Task ScanSoftware()
    {
        // Если уже идет сканирование, пропускаем
        if (IsScanning) return;
        
        IsScanning = true;
        string reportJson = null;
        try
        {
            var scanner = SoftwareScannerFactory.Create();
            reportJson = await scanner.GenerateReportAsync();

            byte[] encryptionKey = SHA256.HashData("32-char-encryption-key-here"u8.ToArray());

            var secureChannel = new SecureChannelService(encryptionKey);
            byte[] encryptedData = await secureChannel.EncryptAndCompressAsync(reportJson);

            var content = new ByteArrayContent(encryptedData);

            string ipIlya = LoadConfiguration() + "/upload-report";
            string ipLocalhost = "http://localhost:8080/upload-report";
            var response = await _httpClient.PostAsync(ipLocalhost, content);
            response.EnsureSuccessStatusCode();

            Report = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Report = $"Error: {ex.Message}\n" + reportJson;
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task SaveReport()
    {
        if (string.IsNullOrEmpty(Report)) return;

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Save Software Report",
            Filters = { new FileDialogFilter { Name = "JSON Files", Extensions = { "json" } } },
            DefaultExtension = "json",
            InitialFileName = $"software_report_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        var window = new Window();
        var result = await saveFileDialog.ShowAsync(window);
        
        if (result != null)
        {
            try
            {
                await File.WriteAllTextAsync(result, Report);
                Report = "Report saved successfully!\n" + Report;
            }
            catch (Exception ex)
            {
                Report = $"Error saving file: {ex.Message}\n" + Report;
            }
        }
    }
    
    private string? LoadConfiguration()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.xml");
    
        if (File.Exists(configPath))
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(configPath);

                return xmlDoc.SelectSingleNode("environment/server")?.InnerText;
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                Console.WriteLine($"Config load error: {ex.Message}");
                return null;
            }
        }
        return null;
    }
}