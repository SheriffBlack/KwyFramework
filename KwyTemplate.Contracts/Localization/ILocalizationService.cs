namespace KwyTemplate.Contracts.Localization;

public interface ILocalizationService
{
    LanguageType CurrentLanguage { get; }

    event EventHandler<LanguageType>? LanguageChanged;

    void Apply(LanguageType languageType);

    string GetString(string key);

    string T(string key, string fallback)
    {
        string value = GetString(key);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal)
            ? fallback
            : value;
    }

    string TF(string key, string fallback, params object?[] args)
        => string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            T(key, fallback),
            args);
}
