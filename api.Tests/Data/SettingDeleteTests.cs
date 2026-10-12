using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Tests.Data;

/// <summary>
/// Regression tests for P2-07, kept by P2-09: deleting a fully populated
/// setting cascades without a foreign key violation and leaves no orphans.
/// Before FixRelationshipDeleteBehavior the relationship-to-entry keys were
/// RESTRICT, which Postgres checks mid-cascade, so the delete depended on the
/// order the cascades ran in. With NO ACTION the order no longer matters.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SettingDeleteTests : PostgresTestBase
{
  public SettingDeleteTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task<Guid> SeedPopulatedSettingAsync(Guid ownerId, Guid playerId)
  {
    var settingId = Guid.NewGuid();
    var first = new SettingEntry
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      Name = "First Entry",
      EntryType = SettingEntryType.Location,
    };
    var second = new SettingEntry
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      Name = "Second Entry",
      EntryType = SettingEntryType.Person,
    };

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(new CampaignSetting { Id = settingId, Name = "Test Setting", OwnerUserId = ownerId });
      db.SettingEntries.AddRange(first, second);
      db.SettingEntryRelationships.AddRange(
          new SettingEntryRelationship
          {
            Id = Guid.NewGuid(),
            CampaignSettingId = settingId,
            SourceEntryId = first.Id,
            TargetEntryId = second.Id,
            RelationshipType = "ConnectedTo",
          },
          new SettingEntryRelationship
          {
            Id = Guid.NewGuid(),
            CampaignSettingId = settingId,
            SourceEntryId = second.Id,
            TargetEntryId = first.Id,
            RelationshipType = "LivesIn",
          });
      db.SettingMemberships.AddRange(
          new SettingMembership { Id = Guid.NewGuid(), CampaignSettingId = settingId, UserId = ownerId, Role = SettingRole.GameMaster },
          new SettingMembership { Id = Guid.NewGuid(), CampaignSettingId = settingId, UserId = playerId, Role = SettingRole.Player });
      return db.SaveChangesAsync();
    });

    return settingId;
  }

  private async Task AssertNothingLeftAsync(Guid settingId)
  {
    Assert.False(await Factory.WithDbAsync(db => db.CampaignSettings.AnyAsync(s => s.Id == settingId)));
    Assert.False(await Factory.WithDbAsync(db => db.SettingEntries.AnyAsync(e => e.CampaignSettingId == settingId)));
    Assert.False(await Factory.WithDbAsync(db => db.SettingEntryRelationships.AnyAsync(r => r.CampaignSettingId == settingId)));
    Assert.False(await Factory.WithDbAsync(db => db.SettingMemberships.AnyAsync(m => m.CampaignSettingId == settingId)));
  }

  [Fact]
  public async Task Database_cascade_deletes_a_populated_setting()
  {
    var owner = await Factory.CreateUserAsync("owner@example.com", "Owner");
    var player = await Factory.CreateUserAsync("player@example.com", "Player");
    var settingId = await SeedPopulatedSettingAsync(owner.Id, player.Id);

    // Only the setting is loaded, so the database does all the cascading.
    await Factory.WithDbAsync(async db =>
    {
      db.CampaignSettings.Remove(await db.CampaignSettings.SingleAsync(s => s.Id == settingId));
      return await db.SaveChangesAsync();
    });

    await AssertNothingLeftAsync(settingId);
    Assert.Equal(2, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }

  [Fact]
  public async Task Owner_DELETE_removes_a_populated_setting_and_its_children()
  {
    var (owner, ownerUser) = await Factory.CreateSignedInClientAsync("owner@example.com", "Owner");
    var player = await Factory.CreateUserAsync("player@example.com", "Player");
    var settingId = await SeedPopulatedSettingAsync(ownerUser.Id, player.Id);

    // Someone else's setting must be untouched.
    var (other, _) = await Factory.CreateSignedInClientAsync("other@example.com", "Other");
    var otherCreated = await other.PostAsJsonAsync("/api/settings", new { name = "Untouched" });
    var otherId = (await JsonAssert.ReadJsonAsync(otherCreated)).GetProperty("id").GetGuid();

    var response = await owner.DeleteAsync($"/api/settings/{settingId}");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    await AssertNothingLeftAsync(settingId);
    Assert.True(await Factory.WithDbAsync(db => db.CampaignSettings.AnyAsync(s => s.Id == otherId)));
    Assert.Equal(1, await Factory.WithDbAsync(db => db.SettingMemberships.CountAsync()));
    Assert.Equal(3, await Factory.WithDbAsync(db => db.Users.CountAsync()));
  }
}
