using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Settings;

[Collection(PostgresCollection.Name)]
public class SettingsUpdateTests : PostgresTestBase
{
  public SettingsUpdateTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Duplicate_name_returns_409_case_insensitive()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var first =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Osepia" });

    var second =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Valeria" });

    Assert.Equal(
        HttpStatusCode.Created,
        first.StatusCode);

    Assert.Equal(
        HttpStatusCode.Created,
        second.StatusCode);

    var firstBody =
        await JsonAssert.ReadJsonAsync(first);

    var firstId =
        firstBody
            .GetProperty("id")
            .GetGuid();

    var response =
        await client.PutAsJsonAsync(
            $"/api/settings/{firstId}",
            new
            {
              name = "VALERIA",
              description = "Renamed"
            });

    Assert.Equal(
        HttpStatusCode.Conflict,
        response.StatusCode);

    await JsonAssert.ReadProblemAsync(response);
  }

  [Fact]
  public async Task Saving_with_own_current_name_succeeds()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var created =
        await client.PostAsJsonAsync(
            "/api/settings",
            new
            {
              name = "Osepia",
              description = "Original"
            });

    Assert.Equal(
        HttpStatusCode.Created,
        created.StatusCode);

    var body =
        await JsonAssert.ReadJsonAsync(created);

    var settingId =
        body
            .GetProperty("id")
            .GetGuid();

    var response =
        await client.PutAsJsonAsync(
            $"/api/settings/{settingId}",
            new
            {
              name = "Osepia",
              description = "Updated"
            });

    Assert.Equal(
        HttpStatusCode.OK,
        response.StatusCode);

    var updated =
        await JsonAssert.ReadJsonAsync(response);

    Assert.Equal(
        "Osepia",
        updated.GetProperty("name").GetString());

    Assert.Equal(
        "Updated",
        updated.GetProperty("description").GetString());
  }

  [Fact]
  public async Task Wildcards_in_name_are_literal()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var first =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Osepia" });

    var second =
        await client.PostAsJsonAsync(
            "/api/settings",
            new { name = "Valeria" });

    Assert.Equal(
        HttpStatusCode.Created,
        first.StatusCode);

    Assert.Equal(
        HttpStatusCode.Created,
        second.StatusCode);

    var secondBody =
        await JsonAssert.ReadJsonAsync(second);

    var secondId =
        secondBody
            .GetProperty("id")
            .GetGuid();

    var response =
        await client.PutAsJsonAsync(
            $"/api/settings/{secondId}",
            new
            {
              name = "O_epia",
              description = "Literal underscore"
            });

    Assert.Equal(
        HttpStatusCode.OK,
        response.StatusCode);
  }
}