using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Kwy.UI.WPF.Controls;

internal sealed class KwyNumberBoxAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
{
    private KwyNumberBox NumberBox => (KwyNumberBox)Owner;

    public KwyNumberBoxAutomationPeer(KwyNumberBox owner) : base(owner)
    {
    }

    bool IRangeValueProvider.IsReadOnly => NumberBox.IsReadOnly || !NumberBox.IsEnabled;

    double IRangeValueProvider.LargeChange => NumberBox.SmallChange;

    double IRangeValueProvider.Maximum => NumberBox.Maximum ?? double.MaxValue;

    double IRangeValueProvider.Minimum => NumberBox.Minimum ?? double.MinValue;

    double IRangeValueProvider.SmallChange => NumberBox.SmallChange;

    double IRangeValueProvider.Value => NumberBox.Value ?? 0d;

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

    protected override string GetClassNameCore() => nameof(KwyNumberBox);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Spinner;

    void IRangeValueProvider.SetValue(double value)
    {
        if (!NumberBox.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (NumberBox.IsReadOnly)
        {
            throw new InvalidOperationException("The number box is read-only.");
        }

        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        NumberBox.Value = value;
    }
}
