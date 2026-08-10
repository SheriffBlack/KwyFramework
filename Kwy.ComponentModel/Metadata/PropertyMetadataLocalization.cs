namespace Kwy.ComponentModel;

/// <summary>
/// Broadcasts that metadata display text should be refreshed.
/// UI layers can trigger this after their resource dictionaries or language resources change.
/// </summary>
public static class PropertyMetadataLocalization
{
    public static event EventHandler? Changed;

    public static void NotifyChanged()
        => Changed?.Invoke(null, EventArgs.Empty);
}
