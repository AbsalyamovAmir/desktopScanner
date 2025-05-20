using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

            var installLocation = subkey?.GetValue("InstallLocation") as string ?? string.Empty;
            string? publisher = subkey?.GetValue("Publisher") as string;
            string? displayVersion = subkey?.GetValue("DisplayVersion") as string;

            // Получаем путь к исполняемому файлу (если указан InstallLocation)
            string? executablePath = GetMainExecutablePath(installLocation, subkey);

            var software = new InstalledSoftware
            {
                Name = displayName,
                Version = displayVersion ?? string.Empty,
                Vendor = publisher ?? string.Empty,
                InstallDate = installDate,
                InstallLocation = installLocation,
                Architecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"
            };

            // Проверяем цифровую подпись и хеш (если есть путь к EXE)
            // if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
            // {
            //     software.IsSigned = CheckDigitalSignature(executablePath, out var signer);
            //     software.SignaturePublisher = signer;
            //     software.FileHash = CalculateFileHash(executablePath);
            // }

            softwareList.Add(software);
        }

        return softwareList;
    }

    private string? GetMainExecutablePath(string installLocation, RegistryKey? subkey)
    {
        // 1. Проверяем UninstallString (например, "C:\Program Files\App\uninstall.exe")
        if (subkey?.GetValue("UninstallString") is string uninstallStr && 
            uninstallStr.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var uninstallExe = uninstallStr.Trim('"');
            if (File.Exists(uninstallExe))
                return uninstallExe;
        }

        // 2. Ищем EXE в папке установки
        if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation))
            return null;

        try
        {
            var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.AllDirectories);
            if (exeFiles.Length == 0)
                return null;

            // Приоритет: выбираем файл с именем, похожим на название программы
            var programName = subkey?.GetValue("DisplayName") as string ?? "";
            var bestMatch = exeFiles.FirstOrDefault(f => 
                Path.GetFileNameWithoutExtension(f).Equals(programName, StringComparison.OrdinalIgnoreCase));

            return bestMatch ?? exeFiles[0]; // Первый EXE, если нет совпадения
        }
        catch
        {
            return null;
        }
    }

    private bool CheckDigitalSignature(string filePath, out string? publisher)
    {
        publisher = null;

        try
        {
            if (!File.Exists(filePath))
                return false;

            var cert = X509Certificate.CreateFromSignedFile(filePath);
            var x509Cert = new X509Certificate2(cert);

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            bool isValid = chain.Build(x509Cert);
            publisher = x509Cert.Subject;

            return isValid;
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"Ошибка проверки подписи для {filePath}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Неизвестная ошибка для {filePath}: {ex.Message}");
            return false;
        }
    }

    private string? CalculateFileHash(string filePath)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> GenerateReportAsync()
    {
        var software = await GetInstalledSoftwareAsync();
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Разрешает вывод кириллицы без экранирования
        };
        return JsonSerializer.Serialize(software, options);
    }
}