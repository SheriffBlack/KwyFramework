using Kwy.Files;
using KwyTemplate.App.Models;
using System.IO;

namespace KwyTemplate.App.Services;

/// <summary>
/// 程序设定持久化入口。程序启动时主动确保配置存在，系统页只负责展示和保存同一份配置。
/// </summary>
public sealed class ProgramSettingsStore
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "System",
        "ProgramSettings.json");

    public string FilePath => SettingsFilePath;

    public ProgramSettingsModel Current { get; private set; } = new();

    public ProgramSettingsLoadResult LoadOrCreate()
    {
        if (!File.Exists(SettingsFilePath))
        {
            Current = new ProgramSettingsModel();
            JsonHelper.Write(SettingsFilePath, Current);
            return new ProgramSettingsLoadResult(Current, SettingsFilePath, true);
        }

        Current = JsonHelper.Read<ProgramSettingsModel>(SettingsFilePath) ?? new ProgramSettingsModel();
        return new ProgramSettingsLoadResult(Current, SettingsFilePath, false);
    }

    public async Task SaveAsync(ProgramSettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await JsonHelper.WriteAsync(SettingsFilePath, settings).ConfigureAwait(false);
        Current = settings;
    }
}

public sealed record ProgramSettingsLoadResult(
    ProgramSettingsModel Settings,
    string FilePath,
    bool Created);
