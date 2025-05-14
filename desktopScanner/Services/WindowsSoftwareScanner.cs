using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using desktopScanner.models;
using Microsoft.Win32;

namespace desktopScanner.Services;

public class WindowsSoftwareScanner : ISoftwareScanner
{
    public async Task<List<InstalledSoftware>> GetInstalledSoftwareAsync()
    {
        var softwareList = new List<InstalledSoftware>();

        // 64-bit software
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            softwareList.AddRange(GetSoftwareFromRegistry(key));
        }

        // 32-bit software on 64-bit OS
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            softwareList.AddRange(GetSoftwareFromRegistry(key));
        }

        // Current user software
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

            DateTime? installDate = null;
            if (subkey?.GetValue("InstallDate") is string installDateStr && 
                DateTime.TryParseExact(installDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                installDate = parsedDate;
            }

            softwareList.Add(new InstalledSoftware
            {
                Name = displayName,
                Version = subkey?.GetValue("DisplayVersion") as string ?? string.Empty,
                Vendor = subkey?.GetValue("Publisher") as string ?? string.Empty,
                InstallDate = installDate,
                InstallLocation = subkey?.GetValue("InstallLocation") as string ?? string.Empty,
                Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"
            });
        }

        return softwareList;
    }

    public async Task<string> GenerateReportAsync()
    {
        var software = await GetInstalledSoftwareAsync();
        return JsonSerializer.Serialize(software, new JsonSerializerOptions { WriteIndented = true });
    }
}