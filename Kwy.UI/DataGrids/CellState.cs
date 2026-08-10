using System.ComponentModel;

namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格单元格状态。
/// </summary>
public class CellState : INotifyPropertyChanged
{
    private object? value;
    private bool? judge;

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

    public bool? Judge
    {
        get => judge;
        set
        {
            if (judge != value)
            {
                judge = value;
                OnPropertyChanged(nameof(Judge));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
