namespace Kwy.Data.Abstractions;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    PageRequest Page)
{
    public int TotalPages => Page.PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)Page.PageSize);
}
