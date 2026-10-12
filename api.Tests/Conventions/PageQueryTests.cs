using Lorebound.Api.Dtos.Common;

namespace Lorebound.Api.Tests.Conventions;

public class PageQueryTests
{
  [Fact]
  public void Defaults_to_first_page_of_twenty()
  {
    var query = new PageQuery();

    Assert.Equal(1, query.Page);
    Assert.Equal(20, query.PageSize);
    Assert.Equal(0, query.Skip());
  }

  [Theory]
  [InlineData(0, 0, 1, 1)]
  [InlineData(-5, 500, 1, 100)]
  [InlineData(3, 50, 3, 50)]
  public void Clamps_out_of_range_values(
      int page, int pageSize, int expectedPage, int expectedPageSize)
  {
    var query = new PageQuery { Page = page, PageSize = pageSize };

    Assert.Equal(expectedPage, query.Page);
    Assert.Equal(expectedPageSize, query.PageSize);
  }

  [Fact]
  public void Skip_accounts_for_page_size()
  {
    Assert.Equal(100, new PageQuery { Page = 3, PageSize = 50 }.Skip());
  }
}
