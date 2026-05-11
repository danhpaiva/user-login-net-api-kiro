namespace UserApiLogin.DTOs;

public class PagedResultDto<T>
{
    /// <summary>Página atual (base 1).</summary>
    public int Page { get; init; }

    /// <summary>Quantidade de itens por página.</summary>
    public int PageSize { get; init; }

    /// <summary>Total de registros no banco.</summary>
    public int TotalItems { get; init; }

    /// <summary>Total de páginas calculado.</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>Indica se existe uma página anterior.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Indica se existe uma próxima página.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Itens da página atual.</summary>
    public IEnumerable<T> Items { get; init; } = [];
}
