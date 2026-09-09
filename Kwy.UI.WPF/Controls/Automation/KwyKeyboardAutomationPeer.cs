using System.Windows.Automation.Peers;

namespace Kwy.UI.WPF.Controls;

internal sealed class KwyKeyboardAutomationPeer : FrameworkElementAutomationPeer
{
    public KwyKeyboardAutomationPeer(KwyKeyboard owner) : base(owner)
    {
    }

    protected override string GetClassNameCore() => nameof(KwyKeyboard);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Pane;

    protected override string GetNameCore()
    {
        string name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? "Soft keyboard" : name;
    }
}
