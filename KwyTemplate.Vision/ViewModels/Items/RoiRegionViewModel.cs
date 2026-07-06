using Kwy.MVVM.Core;
using System.Windows;

namespace KwyTemplate.Vision.ViewModels.Items;

public sealed class RoiRegionViewModel : BindableBase
{
    private string name = "ROI 1";
    private Rect bounds;
    private bool isEnabled = true;

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public string Type => "Rectangle";

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public Rect Bounds
    {
        get => bounds;
        set
        {
            if (SetProperty(ref bounds, value))
            {
                RaisePropertyChanged(nameof(XText));
                RaisePropertyChanged(nameof(YText));
                RaisePropertyChanged(nameof(WidthText));
                RaisePropertyChanged(nameof(HeightText));
                RaisePropertyChanged(nameof(Summary));
            }
        }
    }

    public string XText => Bounds.X.ToString("F0");

    public string YText => Bounds.Y.ToString("F0");

    public string WidthText => Bounds.Width.ToString("F0");

    public string HeightText => Bounds.Height.ToString("F0");

    public string Summary => $"X={XText}, Y={YText}, W={WidthText}, H={HeightText}";
}
