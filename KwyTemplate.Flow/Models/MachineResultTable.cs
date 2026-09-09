using System.Collections.ObjectModel;

namespace KwyTemplate.Flow.Models;

/// <summary>
/// UI-independent state of a machine result cell.
/// </summary>
public enum MachineResultCellState
{
    None,
    Success,
    Error
}

/// <summary>
/// UI-independent result column exposed by a machine.
/// </summary>
public sealed class MachineResultColumn
{
    internal MachineResultColumn(string key, string header)
    {
        Key = key;
        Header = header;
    }

    public string Key { get; }

    public string Header { get; internal set; }
}

/// <summary>
/// UI-independent result cell exposed by a machine.
/// </summary>
public sealed class MachineResultCell
{
    public object? Value { get; internal set; }

    public MachineResultCellState State { get; internal set; }
}

/// <summary>
/// UI-independent result row exposed by a machine. Presentation layers project
/// this read model into their own observable or bindable types.
/// </summary>
public sealed class MachineResultRow
{
    private readonly Dictionary<string, MachineResultCell> cells = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, MachineResultCell> readOnlyCells;

    internal MachineResultRow(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
        readOnlyCells = new ReadOnlyDictionary<string, MachineResultCell>(cells);
    }

    public string Key { get; }

    public string DisplayName { get; internal set; }

    public IReadOnlyDictionary<string, MachineResultCell> Cells => readOnlyCells;

    internal MachineResultCell GetOrAddCell(string key)
    {
        if (!cells.TryGetValue(key, out MachineResultCell? cell))
        {
            cell = new MachineResultCell();
            cells.Add(key, cell);
        }

        return cell;
    }
}
