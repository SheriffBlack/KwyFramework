using Kwy.MVVM.Core;
using KwyTemplate.App.Services;
using KwyTemplate.Contracts.Services;

namespace KwyTemplate.App.ViewModels;

public class LoadViewModel : BindableBase
{
    private readonly ProgramSettingsStore programSettingsStore;

    public LoadViewModel(ProgramSettingsStore programSettingsStore, StartupProgressService startupProgress)
    {
        this.programSettingsStore = programSettingsStore ?? throw new ArgumentNullException(nameof(programSettingsStore));
        StartupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
    }

    public StartupProgressService StartupProgress { get; }

    public string OfficialUrl => programSettingsStore.Current.OfficialUrl;

    public string CopyrightText => programSettingsStore.Current.Copyright;
}
