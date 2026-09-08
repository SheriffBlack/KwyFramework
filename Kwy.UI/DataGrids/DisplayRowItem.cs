using System.ComponentModel;

namespace Kwy.UI.DataGrids;

/// <summary>
/// 通用表格行，使用索引器支持动态列绑定。
/// </summary>
public class DisplayRowItem : INotifyPropertyChanged
{
    private string rowName = string.Empty;
    private readonly Dictionary<string, CellState> cells = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, CellState> readOnlyCells;

    public DisplayRowItem()
    {
        readOnlyCells = new System.Collections.ObjectModel.ReadOnlyDictionary<string, CellState>(cells);
    }

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

    public IReadOnlyDictionary<string, CellState> Cells => readOnlyCells;

    public CellState? this[string key]
        => cells.TryGetValue(key, out CellState? state) ? state : null;

    public static DisplayRowItem CreateRow(object? rowName, params (string Key, object? Value)[] values)
    {
        var row = new DisplayRowItem { RowName = rowName?.ToString() ?? string.Empty };
        foreach ((string key, object? value) in values)
        {
            row.UpdateCell(key, value);
        }

        return row;
    }

    public CellState? GetCell(string key)
        => cells.TryGetValue(key, out CellState? state) ? state : null;

    public CellState GetOrAddCell(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!cells.TryGetValue(key, out CellState? state))
        {
            state = new CellState();
            cells.Add(key, state);
        }

        return state;
    }

    public void UpdateCell(string key, object? value)
        => GetOrAddCell(key).Value = value;

    public void UpdateVisualState(string key, CellValidationState visualState)
        => GetOrAddCell(key).VisualState = visualState;

    public bool RemoveCell(string key) => cells.Remove(key);

    public void ClearCells() => cells.Clear();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
