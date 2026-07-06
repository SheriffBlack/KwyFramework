namespace Kwy.Data.Abstractions;

public readonly record struct PageRequest(int PageIndex, int PageSize)
{
    public int Offset => PageIndex * PageSize;

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
