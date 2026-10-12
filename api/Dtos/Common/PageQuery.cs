using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Dtos.Common;

/// <summary>
/// Paging parameters bound from the query string (<c>?page=2&amp;pageSize=50</c>).
/// Out-of-range values are clamped rather than rejected.
/// </summary>
public record PageQuery
{
  public const int DefaultPageSize = 20;

  public const int MaxPageSize = 100;

  private readonly int _page = 1;

  private readonly int _pageSize = DefaultPageSize;

  // Named explicitly so the OpenAPI document shows the casing clients use.
  [FromQuery(Name = "page")]
  public int Page
  {
    get => _page;
    init => _page = Math.Max(1, value);
  }

  [FromQuery(Name = "pageSize")]
  public int PageSize
  {
    get => _pageSize;
    init => _pageSize = Math.Clamp(value, 1, MaxPageSize);
  }

  /// <summary>
  /// Rows before this page. A method, not a property: the OpenAPI document
  /// lists every property of a query object as a parameter (P8-06).
  /// </summary>
  public int Skip() => (Page - 1) * PageSize;
}
