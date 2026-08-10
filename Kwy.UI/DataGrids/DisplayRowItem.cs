using System.ComponentModel;

namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格行，使用索引器支持动态列绑定。
/// </summary>
public class DisplayRowItem : INotifyPropertyChanged
{
    private string rowName = string.Empty;

    public string RowName
    {
        get => rowName;
        set
        {
            if (rowName != value)
            {
                rowName = value;
                OnPropertyChanged(nameof(RowName));
            }
        }
    }

    public Dictionary<string, CellState> Cells { get; } = new(StringComparer.OrdinalIgnoreCase);

    public CellState this[string key]
    {
        get
        {
            if (!Cells.TryGetValue(key, out CellState? state))
            {
                state = new CellState();
                Cells[key] = state;
            }

            return state;
        }
    }

    public static DisplayRowItem CreateRow(object? rowName, params (string Key, object? Value)[] values)
    {
        var row = new DisplayRowItem { RowName = rowName?.ToString() ?? string.Empty };
        foreach ((string key, object? value) in values)
        {
            row.UpdateCell(key, value);
        }

        return row;
    }

    public CellState? GetCell(string parameterId)
        => Cells.TryGetValue(parameterId, out CellState? state) ? state : null;

    public void UpdateCell(string key, object? value)
        => this[key].Value = value;

    public void UpdateJudge(string key, bool? judge)
        => this[key].Judge = judge;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
