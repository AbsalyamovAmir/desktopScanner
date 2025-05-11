using System;

namespace desktopScanner.models;

public class InstalledSoftware
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public string InstallLocation { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
}