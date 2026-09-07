using System.Globalization;
using System.IO;
using System.Text;

namespace KwyTemplate.Shell.Services;

public sealed class UserVisibleLogFileService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);
    private readonly object syncRoot = new();
    private DateTime lastCleanupDate = DateTime.MinValue;

    public void Add(string level, string message)
    {
        string logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        CleanupExpiredLogs(logDirectory);

        string filePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyyMMdd}.txt");
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

        lock (syncRoot)
        {
            File.AppendAllText(filePath, line + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    private void CleanupExpiredLogs(string logDirectory)
    {
        DateTime today = DateTime.Today;
        if (today - lastCleanupDate < CleanupInterval)
        {
            return;
        }

        lastCleanupDate = today;
        DateTime expireBefore = today.AddDays(-31);
        foreach (string file in Directory.EnumerateFiles(logDirectory, "????????.txt"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(name, "yyyyMMdd", null, DateTimeStyles.None, out DateTime date)
                && date < expireBefore)
            {
                TryDelete(file);
            }
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch
        {
        }
    }
}
