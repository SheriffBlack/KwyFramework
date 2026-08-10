using Kwy.MVVM.Core;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Models;

public class NavigationItemModel : BindableBase
{
    private string displayText = string.Empty;

    public string DisplayText
    {
        get => displayText;
        set => SetProperty(ref displayText, value ?? string.Empty);
    }

    public string LocalizationKey { get; set; } = string.Empty;

    public string ViewName { get; set; } = string.Empty;

    public bool IsVisibility { get; set; } = true;

    /// <summary>
    /// true -> navigation item has icon, false -> text only.
    /// </summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Navigation parameter used to distinguish different instances of the same view.
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// Permission code required to enter this navigation item. Empty means no permission check.
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// Stable key used only by navigation button selected-state binding.
    /// </summary>
    public string NavigationKey => string.IsNullOrWhiteSpace(Parameter) ? ViewName : $"{ViewName}:{Parameter}";

    public void RefreshLocalization(ILocalizationService localizationService)
    {
        if (localizationService == null || string.IsNullOrWhiteSpace(LocalizationKey))
        {
            return;
        }

        string text = localizationService.GetString(LocalizationKey);
        if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, LocalizationKey, StringComparison.Ordinal))
        {
            DisplayText = text;
        }
    }
}
