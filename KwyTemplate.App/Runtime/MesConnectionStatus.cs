using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;
using KwyTemplate.MES.Abstract.Models;

namespace KwyTemplate.App.Runtime;

public sealed class MesConnectionStatus : BindableBase
{
    private readonly ILocalizationService localizationService;
    private MesConnectionState state = MesConnectionState.Offline;
    private string message = string.Empty;

    public MesConnectionStatus(ILocalizationService localizationService)
    {
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        this.localizationService.LanguageChanged += OnLanguageChanged;
    }

    public MesConnectionState State
    {
        get => state;
        set
        {
            if (SetProperty(ref state, value))
            {
                RaisePropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText => State switch
    {
        MesConnectionState.Online => localizationService.T("Home.MesStatus.Online", "MES在线"),
        MesConnectionState.Connecting => localizationService.T("Home.MesStatus.Connecting", "MES连接中"),
        _ => localizationService.T("Home.MesStatus.Offline", "MES离线")
    };

    public string Message
    {
        get => message;
        set => SetProperty(ref message, value ?? string.Empty);
    }

    private void OnLanguageChanged(object? sender, LanguageType languageType)
        => RaisePropertyChanged(nameof(DisplayText));
}
