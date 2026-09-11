using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Retains the standard list selection pattern for a radio-button group.
/// </summary>
internal sealed class KwyRadioButtonGroupAutomationPeer : ListBoxAutomationPeer
{
    public KwyRadioButtonGroupAutomationPeer(KwyRadioButtonGroup owner) : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(KwyRadioButtonGroup);

    protected override string GetNameCore()
    {
        string name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? "Radio button group" : name;
    }
}
