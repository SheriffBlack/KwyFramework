using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Gives an individual toast a readable fallback name when no automation name is supplied.
/// </summary>
internal sealed class KwyToastAutomationPeer : FrameworkElementAutomationPeer
{
    public KwyToastAutomationPeer(KwyToast owner) : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(KwyToast);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Text;

    protected override string GetNameCore()
    {
        string name = base.GetNameCore();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return ((KwyToast)Owner).Content?.ToString() ?? "Notification";
    }
}
