using System.Collections.Generic;
using System.Threading.Tasks;
using desktopScanner.models;

namespace desktopScanner.Services;

public interface ISoftwareScanner
{
    Task<List<InstalledSoftware>> GetInstalledSoftwareAsync();
    Task<string> GenerateReportAsync();
}