using System;

namespace desktopScanner.models;

public class InstalledSoftware
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public string InstallLocation { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    
    // // Новые поля для проверки оригинальности
    // public bool? IsSigned { get; set; }  // Подписано ли ПО
    // public string? SignaturePublisher { get; set; }  // Издатель подписи
    // public string? FileHash { get; set; }  // SHA-256 хеш файла
}