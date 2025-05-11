using System;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using desktopScanner.Services;
using ReactiveUI;

namespace desktopScanner.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private readonly HttpClient _httpClient = new HttpClient();
    private string _report = string.Empty;
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
    }

    private async Task ScanSoftware()
    {
        IsScanning = true;
        try
        {
            // Добавляем токен в заголовки
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", App.AccessToken);
            
            var scanner = SoftwareScannerFactory.Create();
            Report = await scanner.GenerateReportAsync();
        }
        catch (Exception ex)
        {
            Report = $"Error: {ex.Message}";
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
}