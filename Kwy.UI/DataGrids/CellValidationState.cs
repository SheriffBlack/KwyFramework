namespace Kwy.UI.DataGrids;

/// <summary>
/// Describes presentation-level validation feedback for a dynamic cell.
/// Business results must be mapped to this state by the consuming application.
/// </summary>
public enum CellValidationState
{
    None,
    Success,
    Warning,
    Error
}
