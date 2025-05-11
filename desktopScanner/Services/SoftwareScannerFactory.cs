using System;

namespace desktopScanner.Services;

public static class SoftwareScannerFactory
{
    public static ISoftwareScanner Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsSoftwareScanner();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxSoftwareScanner();
        }

        throw new PlatformNotSupportedException("Unsupported operating system");
    }
}