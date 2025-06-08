using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using desktopScanner.models;
using Microsoft.Win32;

namespace desktopScanner.Services;

public class WindowsSoftwareScanner : ISoftwareScanner
{
    public async Task<List<InstalledSoftware>> GetInstalledSoftwareAsync()
    {
        var softwareList = new List<InstalledSoftware>();
    
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            softwareList.AddRange(GetSoftwareFromRegistry(key));
        }
    
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            softwareList.AddRange(GetSoftwareFromRegistry(key));
        }
    
        using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            softwareList.AddRange(GetSoftwareFromRegistry(key));
        }
    
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Product"))
            {
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;

                    var software = new InstalledSoftware
                    {
                        Name = CleanSoftwareName(name),
                        Version = obj["Version"]?.ToString() ?? string.Empty,
                        Vendor = string.Empty,
                    };

                    softwareList.Add(software);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error if needed
            Console.WriteLine($"Error querying Win32_Product: {ex.Message}");
        }

        return softwareList
            .GroupBy(s => s.Name)
            .Select(g => g.First())
            .ToList();
    }

    private List<InstalledSoftware> GetSoftwareFromRegistry(RegistryKey? key)
    {
        var softwareList = new List<InstalledSoftware>();

        if (key == null) return softwareList;

        foreach (var subkeyName in key.GetSubKeyNames())
        {
            using var subkey = key.OpenSubKey(subkeyName);
            var displayName = subkey?.GetValue("DisplayName") as string;
            if (string.IsNullOrEmpty(displayName)) continue;

            string? publisher = subkey?.GetValue("Publisher") as string;
            string? displayVersion = subkey?.GetValue("DisplayVersion") as string;

            var software = new InstalledSoftware
            {
                Name = CleanSoftwareName(displayName),
                Version = displayVersion ?? string.Empty,
                Vendor = publisher ?? string.Empty,
            };

            softwareList.Add(software);
        }

        return softwareList;
    }

    public async Task<string> GenerateReportAsync()
    {
        var report = new
        {
            SystemInfo = GetSystemInfo(),
            InstalledSoftware = await GetInstalledSoftwareAsync()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(report, options);
    }

    private Dictionary<string, string> GetSystemInfo()
    {
        var systemInfo = new Dictionary<string, string>();

        try
        {
            systemInfo["OSName"] = "Windows";
            systemInfo["MachineName"] = LoadConfiguration();
            systemInfo["OSVersion"] = Environment.OSVersion.Version.Major + " " + Environment.OSVersion.Version.Build;
            systemInfo["BitOS"] = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
        }
        catch (Exception ex)
        {
            systemInfo["Error"] = $"Failed to get system info: {ex.Message}";
        }

        return systemInfo;
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

                return xmlDoc.SelectSingleNode("environment/pc")?.InnerText;
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
    
    private string CleanSoftwareName(string displayName)
    {
        // Сохраняем оригинал для специальных случаев
        string originalName = displayName;

        // Особый случай для Visual C++ - сохраняем архитектуру
        if (displayName.Contains("Microsoft Visual C++"))
        {
            // Удаляем только версию (форматы: 10.0.40219, 14.42.34438)
            displayName = Regex.Replace(displayName, @"\s*-\s*\d+(\.\d+)+", "");
            // Удаляем дублирующиеся пробелы
            displayName = Regex.Replace(displayName, @"\s+", " ").Trim();
            return displayName;
        }

        // Общий случай для остальных программ
        // Удаляем версии (форматы: 1.2.3, v5.0, 2023)
        displayName = Regex.Replace(displayName, @"\s*(v?\d+(\.\d+)+(-\d+)*)", "");

        // Удаляем архитектуру (кроме случаев, когда она часть названия)
        if (!displayName.Contains("Visual C++") &&
            !displayName.Contains("Runtime") &&
            !displayName.Contains("Redistributable"))
        {
            // Изменено: теперь корректно обрабатывает скобки вокруг архитектуры
            displayName = Regex.Replace(displayName,
                @"\s*\(?\b(x\d+|arm\d+|64-bit|32-bit|amd64)\b\)?(_[\w-]+)?",
                "",
                RegexOptions.IgnoreCase);
        }

        // Удаляем языковые метки (ru, en-US)
        displayName = Regex.Replace(displayName, @"\s*\b([a-z]{2}(-[A-Z]{2})?)\b", "", RegexOptions.IgnoreCase);

        // Чистим оставшиеся артефакты
        displayName = displayName
            .Replace("()", "")
            .Replace("  ", " ")
            .Trim(' ', '-', '(', ')', '™', '®');

        return displayName;
    }
}