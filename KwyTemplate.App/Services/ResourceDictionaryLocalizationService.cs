using System.Windows;
using Kwy.ComponentModel;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Services;

public sealed class ResourceDictionaryLocalizationService : ILocalizationService
{
    private const string DictionaryMarkerKey = "KwyTemplate.Localization.Dictionary";

    private static readonly IReadOnlyDictionary<LanguageType, Uri> LanguageDictionaries = new Dictionary<LanguageType, Uri>
    {
        [LanguageType.ZH_CN] = new("pack://application:,,,/KwyTemplate.App;component/Resources/Lang/zh-CN.xaml", UriKind.Absolute),
        [LanguageType.ZH_TW] = new("pack://application:,,,/KwyTemplate.App;component/Resources/Lang/zh-TW.xaml", UriKind.Absolute),
        [LanguageType.EN_US] = new("pack://application:,,,/KwyTemplate.App;component/Resources/Lang/en-US.xaml", UriKind.Absolute)
    };

    public LanguageType CurrentLanguage { get; private set; } = LanguageType.ZH_CN;

    public event EventHandler<LanguageType>? LanguageChanged;

    public void Apply(LanguageType languageType)
    {
        if (!LanguageDictionaries.TryGetValue(languageType, out Uri? source))
        {
            languageType = LanguageType.ZH_CN;
            source = LanguageDictionaries[languageType];
        }

        var application = Application.Current;
        if (application == null)
        {
            CurrentLanguage = languageType;
            LanguageChanged?.Invoke(this, languageType);
            PropertyMetadataLocalization.NotifyChanged();
            return;
        }

        void applyCore()
        {
            var dictionaries = application.Resources.MergedDictionaries;
            ResourceDictionary? oldDictionary = dictionaries.FirstOrDefault(IsLocalizationDictionary);
            if (oldDictionary != null)
            {
                dictionaries.Remove(oldDictionary);
            }

            var newDictionary = new ResourceDictionary { Source = source };
            newDictionary[DictionaryMarkerKey] = true;
            dictionaries.Add(newDictionary);
            CurrentLanguage = languageType;
            LanguageChanged?.Invoke(this, languageType);
            PropertyMetadataLocalization.NotifyChanged();
        }

        if (application.Dispatcher.CheckAccess())
        {
            applyCore();
            return;
        }

        application.Dispatcher.Invoke(applyCore);
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        object? value = Application.Current?.TryFindResource(key);
        return value?.ToString() ?? key;
    }

    private static bool IsLocalizationDictionary(ResourceDictionary dictionary)
        => dictionary.Contains(DictionaryMarkerKey)
            || dictionary.Source?.OriginalString.Contains("/Resources/Lang/", StringComparison.OrdinalIgnoreCase) == true;
}