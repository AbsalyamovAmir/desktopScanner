using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
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
                    Vendor = parts[3]
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
                    Vendor = parts[2]
                });
            }
        }

        return softwareList;
    }

    public async Task<string> GenerateReportAsync()
    {
        var report = new
        {
            SystemInfo = GetLinuxSystemInfo(),
            InstalledSoftware = await GetInstalledSoftwareAsync()
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private Dictionary<string, string> GetLinuxSystemInfo()
    {
        var systemInfo = new Dictionary<string, string>();

        try
        {
            // Базовая информация через Environment
            systemInfo["OSName"] = Environment.OSVersion.VersionString;
            systemInfo["MachineName"] = LoadConfiguration();
            systemInfo["OSVersion"] = Environment.OSVersion.Version.Major + " " + Environment.OSVersion.Version.Build;
            systemInfo["BitOS"] = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";

            // Получаем информацию о дистрибутиве Linux
            if (File.Exists("/etc/os-release"))
            {
                var osRelease = File.ReadAllLines("/etc/os-release");
                foreach (var line in osRelease)
                {
                    if (line.StartsWith("PRETTY_NAME="))
                    {
                        systemInfo["OSName"] = line.Split('=')[1].Trim('"');
                    }
                    else if (line.StartsWith("VERSION_ID="))
                    {
                        systemInfo["OSVersionId"] = line.Split('=')[1].Trim('"');
                    }
                }
            }

            // Информация о процессоре
            if (File.Exists("/proc/cpuinfo"))
            {
                var cpuInfo = File.ReadAllText("/proc/cpuinfo");
                var modelName = cpuInfo.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("model name"))?
                    .Split(':')[1]
                    .Trim();

                if (!string.IsNullOrEmpty(modelName))
                {
                    systemInfo["Processor"] = modelName;
                }

                var cores = cpuInfo.Split('\n')
                    .Count(line => line.StartsWith("processor"));
                
                if (cores > 0)
                {
                    systemInfo["ProcessorCores"] = cores.ToString();
                }
            }

            // Информация о памяти
            if (File.Exists("/proc/meminfo"))
            {
                var memInfo = File.ReadAllText("/proc/meminfo");
                var totalMem = memInfo.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("MemTotal"))?
                    .Split(':')[1]
                    .Trim();

                if (!string.IsNullOrEmpty(totalMem))
                {
                    systemInfo["TotalMemory"] = totalMem;
                }

                var freeMem = memInfo.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("MemFree"))?
                    .Split(':')[1]
                    .Trim();

                if (!string.IsNullOrEmpty(freeMem))
                {
                    systemInfo["FreeMemory"] = freeMem;
                }
            }

            // Информация о дисках
            var dfProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/df",
                    Arguments = "-h",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            dfProcess.Start();
            var dfOutput = dfProcess.StandardOutput.ReadToEnd();
            dfProcess.WaitForExit();

            systemInfo["DiskInfo"] = dfOutput;

            // Информация о времени работы системы
            var uptimeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/uptime",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            uptimeProcess.Start();
            var uptimeOutput = uptimeProcess.StandardOutput.ReadToEnd();
            uptimeProcess.WaitForExit();

            systemInfo["Uptime"] = uptimeOutput.Trim();
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