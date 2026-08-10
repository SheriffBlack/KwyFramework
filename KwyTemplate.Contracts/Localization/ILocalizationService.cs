namespace KwyTemplate.Contracts.Localization;

public interface ILocalizationService
{
    LanguageType CurrentLanguage { get; }

    event EventHandler<LanguageType>? LanguageChanged;

    void Apply(LanguageType languageType);

    string GetString(string key);
}