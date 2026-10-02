namespace Kwy.Data.Abstractions;

/// <summary>
/// 表示从 0 开始索引的分页请求。
/// </summary>
/// <param name="PageIndex">页索引，首页为 0。</param>
/// <param name="PageSize">每页最大记录数。</param>
public readonly record struct PageRequest(int PageIndex, int PageSize)
{
    /// <summary>获取 OFFSET 分页所需的长整型偏移量。</summary>
    public long Offset => checked((long)PageIndex * PageSize);

    /// <summary>验证页索引和页大小。</summary>
    public void Validate()
    {
        if (PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PageIndex), PageIndex, "Page index must be greater than or equal to 0.");
        }

        if (PageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PageSize), PageSize, "Page size must be greater than 0.");
        }
    }
}
