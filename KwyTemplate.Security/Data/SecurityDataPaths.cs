using System.IO;

namespace KwyTemplate.Security.Data;

public static class SecurityDataPaths
{
    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static string DatabasePath => Path.Combine(DataDirectory, "security.db");

    public static string CreateConnectionString() => $"Data Source={DatabasePath}";
}
