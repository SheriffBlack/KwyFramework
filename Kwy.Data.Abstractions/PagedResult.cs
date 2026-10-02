namespace Kwy.Data.Abstractions;

/// <summary>
/// 表示一页业务数据及完整结果集的统计信息。
/// </summary>
/// <typeparam name="T">页内业务对象类型。</typeparam>
/// <param name="Items">当前页已映射的业务对象。</param>
/// <param name="TotalCount">不考虑分页时的总记录数。</param>
/// <param name="Page">生成当前页的分页请求。</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    PageRequest Page)
{
    /// <summary>根据总记录数和页大小计算总页数。</summary>
    public long TotalPages => Page.PageSize <= 0 || TotalCount <= 0
        ? 0
        : TotalCount / Page.PageSize + (TotalCount % Page.PageSize == 0 ? 0 : 1);
}
