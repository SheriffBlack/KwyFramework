using System.Windows;

namespace Kwy.UI.WPF.Themes;

/// <summary>
/// Applies Kwy color resources to an application or an isolated resource dictionary.
/// </summary>
public static class KwyThemeManager
{
    private const string ThemePathPrefix = "/Kwy.UI.WPF;component/Themes/";
    public static KwyTheme? CurrentTheme { get; private set; }

    /// <summary>
    /// Applies a theme to <see cref="Application.Current"/>. Returns false when no application exists.
    /// </summary>
    public static bool ApplyTheme(KwyTheme theme)
    {
        Application? application = Application.Current;
        if (application == null)
        {
            return false;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            return application.Dispatcher.Invoke(() => ApplyTheme(theme));
        }

        ReplaceThemeDictionary(application.Resources, theme);
        CurrentTheme = theme;
        return true;
    }

    /// <summary>
    /// Applies a theme to the supplied resources, allowing controls to be themed without an application.
    /// The caller must access the dictionary from its owning UI thread.
    /// </summary>
    public static void ApplyTheme(ResourceDictionary resources, KwyTheme theme)
    {
        ArgumentNullException.ThrowIfNull(resources);

        ReplaceThemeDictionary(resources, theme);
    }

    private static void ReplaceThemeDictionary(ResourceDictionary resources, KwyTheme theme)
    {
        Uri source = CreateThemeUri(theme);

        for (int index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (IsKwyThemeDictionary(resources.MergedDictionaries[index]))
            {
                resources.MergedDictionaries.RemoveAt(index);
            }
        }

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = source });
    }

    private static Uri CreateThemeUri(KwyTheme theme)
        => new($"{ThemePathPrefix}{theme}Theme.xaml", UriKind.Relative);

    private static bool IsKwyThemeDictionary(ResourceDictionary dictionary)
    {
        string? source = dictionary.Source?.OriginalString.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        int queryIndex = source.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            source = source[..queryIndex];
        }

        bool isKwyThemePath = source.Contains(
            "/Kwy.UI.WPF;component/Themes/",
            StringComparison.OrdinalIgnoreCase);
        bool isThemeFile = source.EndsWith("/LightTheme.xaml", StringComparison.OrdinalIgnoreCase)
            || source.EndsWith("/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase);
        return isKwyThemePath && isThemeFile;
    }
}
