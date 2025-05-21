using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
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

        return softwareList;
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
                Name = displayName,
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
            systemInfo["OSName"] = Environment.OSVersion.VersionString;
            systemInfo["MachineName"] = LoadConfiguration();
            systemInfo["OSVersion"] = Environment.Version.ToString();
            systemInfo["BitOS"] = Environment.Is64BitOperatingSystem ? "64" : "32";
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
}