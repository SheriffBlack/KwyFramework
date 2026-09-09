using Kwy.UI.DataGrids;
using Kwy.UI.WPF.Controls.Helpers;
using KwyTemplate.Flow.Machines;
using KwyTemplate.Flow.Models;
using System.Collections.ObjectModel;

namespace KwyTemplate.App.Models;

/// <summary>
/// Projects the machine result read model into WPF-bindable rows and columns.
/// All calls are made by the owning view model on the UI thread.
/// </summary>
internal sealed class MachineResultTableAdapter
{
    private readonly Dictionary<string, DisplayRowItem> rowsByKey = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<IDataGridColumnDescriptor> Columns { get; } = [];

    public ObservableCollection<DisplayRowItem> Rows { get; } = [];

    public void Synchronize(IMachineResultProvider source)
    {
        ArgumentNullException.ThrowIfNull(source);
        SynchronizeColumns(source.ResultColumns);
        SynchronizeRows(source.ResultRows);
    }

    private void SynchronizeColumns(IReadOnlyList<MachineResultColumn> source)
    {
        bool structureChanged = Columns.Count != source.Count
            || Columns.Select(static column => column.Key)
                .Where((key, index) => !string.Equals(key, source[index].Key, StringComparison.OrdinalIgnoreCase))
                .Any();

        if (structureChanged)
        {
            Columns.Clear();
            foreach (MachineResultColumn column in source)
            {
                Columns.Add(CreateColumn(column));
            }

            return;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (!Equals(Columns[index].Header, source[index].Header))
            {
                Columns[index] = CreateColumn(source[index]);
            }
        }
    }

    private void SynchronizeRows(IReadOnlyList<MachineResultRow> source)
    {
        var activeKeys = new HashSet<string>(source.Select(static row => row.Key), StringComparer.OrdinalIgnoreCase);
        foreach (string staleKey in rowsByKey.Keys.Where(key => !activeKeys.Contains(key)).ToArray())
        {
            rowsByKey.Remove(staleKey);
        }

        var orderedRows = new List<DisplayRowItem>(source.Count);
        foreach (MachineResultRow resultRow in source)
        {
            if (!rowsByKey.TryGetValue(resultRow.Key, out DisplayRowItem? row))
            {
                row = new DisplayRowItem();
                rowsByKey.Add(resultRow.Key, row);
            }

            row.RowName = resultRow.DisplayName;
            SynchronizeCells(row, resultRow.Cells);
            orderedRows.Add(row);
        }

        if (!Rows.SequenceEqual(orderedRows))
        {
            Rows.Clear();
            foreach (DisplayRowItem row in orderedRows)
            {
                Rows.Add(row);
            }
        }
    }

    private static void SynchronizeCells(
        DisplayRowItem target,
        IReadOnlyDictionary<string, MachineResultCell> source)
    {
        foreach (string staleKey in target.Cells.Keys.Where(key => !source.ContainsKey(key)).ToArray())
        {
            target.RemoveCell(staleKey);
        }

        foreach ((string key, MachineResultCell resultCell) in source)
        {
            CellState cell = target.GetOrAddCell(key);
            cell.Value = resultCell.Value;
            cell.VisualState = resultCell.State switch
            {
                MachineResultCellState.Success => CellValidationState.Success,
                MachineResultCellState.Error => CellValidationState.Error,
                _ => CellValidationState.None
            };
        }
    }

    private static IDataGridColumnDescriptor CreateColumn(MachineResultColumn column)
        => string.Equals(column.Key, "RowName", StringComparison.OrdinalIgnoreCase)
            ? new DataGridColumnDescriptor
            {
                Key = column.Key,
                Header = column.Header,
                BindingPath = nameof(DisplayRowItem.RowName)
            }
            : new DynamicCellColumnDescriptor(column.Key, column.Header);
}
