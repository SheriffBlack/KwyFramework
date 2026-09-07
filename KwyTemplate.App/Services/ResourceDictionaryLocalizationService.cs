using System.Windows;
using System.Windows.Threading;
using Kwy.ComponentModel;
using KwyTemplate.Contracts.Localization;

namespace KwyTemplate.App.Services;

public sealed class ResourceDictionaryLocalizationService : ILocalizationService
{
    private const string DictionaryMarkerKey = "KwyTemplate.Localization.Dictionary";
    private int languageChangeVersion;

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
            if (CurrentLanguage == languageType)
            {
                return;
            }

            CurrentLanguage = languageType;
            LanguageChanged?.Invoke(this, languageType);
            PropertyMetadataLocalization.NotifyChanged();
            return;
        }

        void applyCore()
        {
            if (CurrentLanguage == languageType)
            {
                return;
            }

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
            int changeVersion = ++languageChangeVersion;

            // 资源字典需立即替换，但订阅者包含表格列、导航和属性编辑器的刷新。
            // 延后且合并这些显示刷新，避免一次语言切换独占 UI 消息循环。
            _ = application.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (changeVersion != languageChangeVersion || CurrentLanguage != languageType)
                {
                    return;
                }

                LanguageChanged?.Invoke(this, languageType);
                PropertyMetadataLocalization.NotifyChanged();
            }));
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
