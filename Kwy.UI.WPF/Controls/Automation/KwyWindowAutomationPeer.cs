using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

/// <summary>
/// Exposes <see cref="KwyWindow"/> as a window to UI Automation clients.
/// </summary>
internal sealed class KwyWindowAutomationPeer : FrameworkElementAutomationPeer
{
    public KwyWindowAutomationPeer(KwyWindow owner) : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(KwyWindow);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Window;

    protected override string GetNameCore()
    {
        string name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? ((KwyWindow)Owner).Title : name;
    }
}
