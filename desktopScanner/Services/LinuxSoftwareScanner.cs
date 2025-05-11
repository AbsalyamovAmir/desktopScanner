using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using desktopScanner.models;

namespace desktopScanner.Services;

public class LinuxSoftwareScanner : ISoftwareScanner
{
    public async Task<List<InstalledSoftware>> GetInstalledSoftwareAsync()
    {
        if (File.Exists("/usr/bin/dpkg"))
        {
            return await GetDpkgPackagesAsync();
        }

        if (File.Exists("/usr/bin/rpm"))
        {
            return await GetRpmPackagesAsync();
        }

        throw new NotSupportedException("Only dpkg and rpm package managers are supported on Linux");
    }

    private async Task<List<InstalledSoftware>> GetDpkgPackagesAsync()
    {
        var softwareList = new List<InstalledSoftware>();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/dpkg",
                Arguments = "-l",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        using var reader = new StringReader(output);
        while (await reader.ReadLineAsync() is { } line)
        {
            if (!line.StartsWith("ii ")) continue;
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                softwareList.Add(new InstalledSoftware
                {
                    Name = parts[1],
                    Version = parts[2],
                    Publisher = parts[3],
                    Architecture = parts.Length > 4 ? parts[4] : string.Empty
                });
            }
        }

        return softwareList;
    }

    private async Task<List<InstalledSoftware>> GetRpmPackagesAsync()
    {
        var softwareList = new List<InstalledSoftware>();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/rpm",
                Arguments = "-qa --queryformat '%{NAME}\t%{VERSION}\t%{VENDOR}\t%{ARCH}\n'",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        using var reader = new StringReader(output);
        while (await reader.ReadLineAsync() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length >= 4)
            {
                softwareList.Add(new InstalledSoftware
                {
                    Name = parts[0],
                    Version = parts[1],
                    Publisher = parts[2],
                    Architecture = parts[3]
                });
            }
        }

        return softwareList;
    }

    public async Task<string> GenerateReportAsync()
    {
        var software = await GetInstalledSoftwareAsync();
        return JsonSerializer.Serialize(software, new JsonSerializerOptions { WriteIndented = true });
    }
}