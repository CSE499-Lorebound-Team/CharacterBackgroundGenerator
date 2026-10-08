using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Settings;

[Collection(PostgresCollection.Name)]
public class SettingsCreateTests : PostgresTestBase
{
  public SettingsCreateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Duplicate_name_is_409_case_insensitive()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var first =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Osepia" });

    Assert.Equal(
        HttpStatusCode.Created,
        first.StatusCode);

    var duplicate =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "OSEPIA" });

    Assert.Equal(
        HttpStatusCode.Conflict,
        duplicate.StatusCode);

    await JsonAssert.ReadProblemAsync(
        duplicate);
  }

  [Fact]
  public async Task Underscore_in_name_is_not_a_wildcard()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var first =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Osepia" });

    Assert.Equal(
        HttpStatusCode.Created,
        first.StatusCode);

    var second =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "O_epia" });

    Assert.Equal(
        HttpStatusCode.Created,
        second.StatusCode);
  }

  [Fact]
  public async Task Percent_in_name_is_not_a_wildcard()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var first =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Osepia" });

    Assert.Equal(
        HttpStatusCode.Created,
        first.StatusCode);

    var second =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "O%sepia" });

    Assert.Equal(
        HttpStatusCode.Created,
        second.StatusCode);
  }

  [Fact]
  public async Task Empty_name_is_400()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var response =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "" });

    Assert.Equal(
        HttpStatusCode.BadRequest,
        response.StatusCode);

    await JsonAssert.ReadProblemAsync(
        response);
  }
}