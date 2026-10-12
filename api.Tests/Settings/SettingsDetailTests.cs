using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Settings;

[Collection(PostgresCollection.Name)]
public class SettingsDetailTests : PostgresTestBase
{
  public SettingsDetailTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Entry_counts_by_type_match_seeded_entries()
  {
    var (client, _) =
        await Factory.CreateSignedInClientAsync();

    var created =
        await client.PostAsJsonAsync(
            "/api/settings",
            new
            {
              name = "Osepia",
              description = "Ancient world setting"
            });

    Assert.Equal(
        HttpStatusCode.Created,
        created.StatusCode);

    var createdBody =
        await JsonAssert.ReadJsonAsync(created);

    var settingId =
        createdBody
            .GetProperty("id")
            .GetGuid();

    await Factory.WithDbAsync(async db =>
{
  db.SettingEntries.AddRange(
      new SettingEntry
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        Name = "Aster",
        EntryType = SettingEntryType.Location,
      },
      new SettingEntry
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        Name = "Namar",
        EntryType = SettingEntryType.Location,
      },
      new SettingEntry
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        Name = "Temple of the Dawn",
        EntryType = SettingEntryType.Religion,
      },
      new SettingEntry
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        Name = "The Ashen Compact",
        EntryType = SettingEntryType.Faction,
      });

  return await db.SaveChangesAsync();
});

    var response =
        await client.GetAsync(
            $"/api/settings/{settingId}");

    Assert.Equal(
        HttpStatusCode.OK,
        response.StatusCode);

    var body =
        await JsonAssert.ReadJsonAsync(response);

    var counts =
        body.GetProperty("entryCountsByType");

    Assert.Equal(
        2,
        counts
            .GetProperty("Location")
            .GetInt32());

    Assert.Equal(
        1,
        counts
            .GetProperty("Religion")
            .GetInt32());

    Assert.Equal(
        1,
        counts
            .GetProperty("Faction")
            .GetInt32());

    Assert.False(
        counts.TryGetProperty(
            "Culture",
            out _));
  }
}