using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Makes the transient notification collection discoverable to assistive tools.
/// </summary>
internal sealed class KwyToastHostAutomationPeer : FrameworkElementAutomationPeer
{
    public KwyToastHostAutomationPeer(KwyToastHost owner) : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(KwyToastHost);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.List;

    protected override string GetNameCore()
    {
        string name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? "Notifications" : name;
    }
}
