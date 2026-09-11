using System.Windows;

namespace Kwy.UI.WPF.Themes;

/// <summary>
/// Applies Kwy control-density resources to an application or an isolated resource dictionary.
/// </summary>
public static class KwyDensityManager
{
    private const string DensityPathPrefix = "/Kwy.UI.WPF;component/Themes/Densities/";

    public static KwyDensity? CurrentDensity { get; private set; }

    /// <summary>
    /// Applies a density to <see cref="Application.Current"/>. Returns false when no application exists.
    /// </summary>
    public static bool ApplyDensity(KwyDensity density)
    {
        Application? application = Application.Current;
        if (application == null)
        {
            return false;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            return application.Dispatcher.Invoke(() => ApplyDensity(density));
        }

        ReplaceDensityDictionary(application.Resources, density);
        CurrentDensity = density;
        return true;
    }

    /// <summary>
    /// Applies a density to the supplied resources without requiring an application instance.
    /// The caller must access the dictionary from its owning UI thread.
    /// </summary>
    public static void ApplyDensity(ResourceDictionary resources, KwyDensity density)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ReplaceDensityDictionary(resources, density);
    }

    private static void ReplaceDensityDictionary(ResourceDictionary resources, KwyDensity density)
    {
        for (int index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (IsKwyDensityDictionary(resources.MergedDictionaries[index]))
            {
                resources.MergedDictionaries.RemoveAt(index);
            }
        }

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = CreateDensityUri(density) });
    }

    private static Uri CreateDensityUri(KwyDensity density)
        => new($"{DensityPathPrefix}{density}.xaml", UriKind.Relative);

    private static bool IsKwyDensityDictionary(ResourceDictionary dictionary)
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

        return source.Contains(DensityPathPrefix, StringComparison.OrdinalIgnoreCase)
            && (source.EndsWith("/Compact.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("/Normal.xaml", StringComparison.OrdinalIgnoreCase)
                || source.EndsWith("/Touch.xaml", StringComparison.OrdinalIgnoreCase));
    }
}
