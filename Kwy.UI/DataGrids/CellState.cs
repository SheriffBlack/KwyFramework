using System.ComponentModel;

namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格单元格状态。
/// </summary>
public class CellState : INotifyPropertyChanged
{
    private object? value;
    private CellValidationState visualState;

    public object? Value
    {
        get => value;
        set
        {
            if (!Equals(this.value, value))
            {
                this.value = value;
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    public CellValidationState VisualState
    {
        get => visualState;
        set
        {
            if (visualState != value)
            {
                visualState = value;
                OnPropertyChanged(nameof(VisualState));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
