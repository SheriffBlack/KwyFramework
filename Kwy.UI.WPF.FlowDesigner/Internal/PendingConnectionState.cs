using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Kwy.UI.WPF.FlowDesigner.Internal;

/// <summary>
/// 正在创建的连线的瞬时视图状态，仅供流程编辑器模板绑定。
/// </summary>
internal sealed class PendingConnectionState : INotifyPropertyChanged
{
    private Point source;
    private Point target;
    private string side = "Right";
    private string targetSide = "Left";

    public Point Source
    {
        get => source;
        set => SetProperty(ref source, value);
    }

    public Point Target
    {
        get => target;
        set => SetProperty(ref target, value);
    }

    public string Side
    {
        get => side;
        set => SetProperty(ref side, value);
    }

    public string TargetSide
    {
        get => targetSide;
        set => SetProperty(ref targetSide, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
