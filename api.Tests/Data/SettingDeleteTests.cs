using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lorebound.Api.Tests.Data;

[Collection(PostgresCollection.Name)]
public class SettingDeleteTests
{
  private readonly PostgresFixture _postgres;

  public SettingDeleteTests(
      PostgresFixture postgres)
  {
    _postgres = postgres;
  }

  [Fact]
  public async Task Deleting_setting_with_entries_and_relationships_succeeds()
  {
    await _postgres.ResetAsync();

    using var scope =
        _postgres.Factory.Services.CreateScope();

    var db =
        scope.ServiceProvider
            .GetRequiredService<LoreboundDbContext>();

    var user = new ApplicationUser
    {
      Id = Guid.NewGuid(),
      UserName = "owner@example.com",
      NormalizedUserName = "OWNER@EXAMPLE.COM",
      Email = "owner@example.com",
      NormalizedEmail = "OWNER@EXAMPLE.COM",
      DisplayName = "Setting Owner",
      SecurityStamp = Guid.NewGuid().ToString(),
    };

    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = "Test Setting",
      OwnerUserId = user.Id,
      Owner = user,
    };

    var firstEntry = new SettingEntry
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = setting.Id,
      CampaignSetting = setting,
      Name = "First Entry",
      EntryType = SettingEntryType.Location,
    };

    var secondEntry = new SettingEntry
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = setting.Id,
      CampaignSetting = setting,
      Name = "Second Entry",
      EntryType = SettingEntryType.Location,
    };

    var relationship = new SettingEntryRelationship
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = setting.Id,
      CampaignSetting = setting,
      SourceEntryId = firstEntry.Id,
      SourceEntry = firstEntry,
      TargetEntryId = secondEntry.Id,
      TargetEntry = secondEntry,
      RelationshipType = "ConnectedTo",
    };

    db.Users.Add(user);
    db.CampaignSettings.Add(setting);
    db.SettingEntries.AddRange(
        firstEntry,
        secondEntry);
    db.SettingEntryRelationships.Add(
        relationship);

    await db.SaveChangesAsync();

    db.CampaignSettings.Remove(setting);

    await db.SaveChangesAsync();

    Assert.False(
        await db.CampaignSettings
            .AnyAsync(item =>
                item.Id == setting.Id));

    Assert.False(
        await db.SettingEntries
            .AnyAsync(item =>
                item.CampaignSettingId ==
                setting.Id));

    Assert.False(
        await db.SettingEntryRelationships
            .AnyAsync(item =>
                item.CampaignSettingId ==
                setting.Id));
  }
}