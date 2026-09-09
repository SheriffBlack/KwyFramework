using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Kwy.UI.WPF.Controls;

internal sealed class KwyPercentAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
{
    private KwyPercent Percent => (KwyPercent)Owner;

    public KwyPercentAutomationPeer(KwyPercent owner) : base(owner)
    {
    }

    bool IRangeValueProvider.IsReadOnly => true;
    double IRangeValueProvider.LargeChange => 0d;
    double IRangeValueProvider.Maximum => Math.Max(0d, Percent.Total);
    double IRangeValueProvider.Minimum => 0d;
    double IRangeValueProvider.SmallChange => 0d;
    double IRangeValueProvider.Value => Math.Clamp(Percent.Current, 0d, Math.Max(0d, Percent.Total));

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

    protected override string GetClassNameCore() => nameof(KwyPercent);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.ProgressBar;

    void IRangeValueProvider.SetValue(double value)
        => throw new InvalidOperationException("The percentage display is read-only.");
}
