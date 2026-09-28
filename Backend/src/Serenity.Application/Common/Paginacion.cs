namespace Serenity.Application.Common;

public sealed record PaginationParams(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    /// <summary>Normaliza a rangos válidos (página ≥ 1, tamaño 1..100).</summary>
    public PaginationParams WithClamp()
        => new(Math.Max(1, Page), Math.Clamp(PageSize, 1, MaxPageSize));

    public int Offset => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
