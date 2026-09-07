using System.IO;
using Kwy.Files;
using KwyTemplate.App.Models;

namespace KwyTemplate.App.Services;

/// <summary>
/// Persists mark print options for machines that support mark printing.
/// </summary>
public sealed class MarkPrintOptionsStore
{
    private static readonly string OptionsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "MarkPrint",
        "MarkPrintOptions.json");

    public string FilePath => OptionsFilePath;

    public MarkPrintOptions Current { get; private set; } = new();

    /// <summary>当前编带字符配置已成功保存并替换。</summary>
    public event EventHandler? OptionsChanged;

    public MarkPrintOptionsLoadResult LoadOrCreate()
    {
        if (!File.Exists(OptionsFilePath))
        {
            Current = new MarkPrintOptions();
            JsonHelper.Write(OptionsFilePath, Current);
            OptionsChanged?.Invoke(this, EventArgs.Empty);
            return new MarkPrintOptionsLoadResult(Current, OptionsFilePath, true);
        }

        Current = JsonHelper.Read<MarkPrintOptions>(OptionsFilePath) ?? new MarkPrintOptions();
        return new MarkPrintOptionsLoadResult(Current, OptionsFilePath, false);
    }

    public async Task SaveAsync(MarkPrintOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        await JsonHelper.WriteAsync(OptionsFilePath, options).ConfigureAwait(false);
        Current = options;
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record MarkPrintOptionsLoadResult(
    MarkPrintOptions Options,
    string FilePath,
    bool Created);
