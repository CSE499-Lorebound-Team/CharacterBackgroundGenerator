using Lorebound.Api.Data;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Tests.Data;

// P5-01: the database itself rejects self-links, duplicate links and
// over-long relationship text. P5-05 made the type comparison ignore case.
[Collection(PostgresCollection.Name)]
public class SettingEntryRelationshipTests : PostgresTestBase
{
  public SettingEntryRelationshipTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task<(Guid SettingId, Guid SharnId, Guid BrelandId)> CreateWorldAsync()
  {
    var owner = await Factory.CreateUserAsync("owner@example.com", "Owner");

    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = "Eberron",
      OwnerUserId = owner.Id,
    };
    var sharn = Entry(setting.Id, "Sharn");
    var breland = Entry(setting.Id, "Breland");

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(setting);
      db.SettingEntries.AddRange(sharn, breland);
      return db.SaveChangesAsync();
    });

    return (setting.Id, sharn.Id, breland.Id);
  }

  private static SettingEntry Entry(Guid settingId, string name) =>
      new()
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        Name = name,
        EntryType = SettingEntryType.Location,
      };

  private static SettingEntryRelationship Link(
      Guid settingId,
      Guid sourceId,
      Guid targetId,
      string type = "located_in",
      string? description = null) =>
      new()
      {
        Id = Guid.NewGuid(),
        CampaignSettingId = settingId,
        SourceEntryId = sourceId,
        TargetEntryId = targetId,
        RelationshipType = type,
        Description = description,
      };

  private Task<int> AddAsync(params SettingEntryRelationship[] links) =>
      Factory.WithDbAsync(db =>
      {
        db.SettingEntryRelationships.AddRange(links);
        return db.SaveChangesAsync();
      });

  private async Task<PostgresException> AssertRejectedAsync(SettingEntryRelationship link)
  {
    var error = await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(link));
    return Assert.IsType<PostgresException>(error.InnerException);
  }

  [Fact]
  public async Task A_self_link_violates_the_check_constraint()
  {
    var (settingId, sharnId, _) = await CreateWorldAsync();

    var postgres = await AssertRejectedAsync(Link(settingId, sharnId, sharnId));

    Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
    Assert.Equal("CK_SettingEntryRelationships_NoSelfLink", postgres.ConstraintName);
  }

  [Fact]
  public async Task A_duplicate_link_violates_the_unique_index()
  {
    var (settingId, sharnId, brelandId) = await CreateWorldAsync();
    await AddAsync(Link(settingId, sharnId, brelandId));

    var postgres = await AssertRejectedAsync(Link(settingId, sharnId, brelandId));

    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    Assert.Equal("IX_SettingEntryRelationships_Source_Target_Type", postgres.ConstraintName);
  }

  [Fact]
  public async Task Another_type_or_the_reverse_direction_is_not_a_duplicate()
  {
    var (settingId, sharnId, brelandId) = await CreateWorldAsync();

    await AddAsync(
        Link(settingId, sharnId, brelandId, "located_in"),
        Link(settingId, sharnId, brelandId, "capital_of"),
        Link(settingId, brelandId, sharnId, "located_in"));

    Assert.Equal(3, await Factory.WithDbAsync(db => db.SettingEntryRelationships.CountAsync()));
  }

  [Fact]
  public async Task RelationshipType_is_limited_to_60_characters()
  {
    var (settingId, sharnId, brelandId) = await CreateWorldAsync();
    await AddAsync(Link(settingId, sharnId, brelandId, new string('a', 60)));

    var postgres = await AssertRejectedAsync(
        Link(settingId, brelandId, sharnId, new string('a', 61)));

    Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
    Assert.Equal("CK_SettingEntryRelationships_RelationshipTypeLength", postgres.ConstraintName);
  }

  // P5-05: types are citext, so a link differing only by the case of its
  // type is a duplicate.
  [Fact]
  public async Task A_type_differing_only_by_case_is_a_duplicate()
  {
    var (settingId, sharnId, brelandId) = await CreateWorldAsync();
    await AddAsync(Link(settingId, sharnId, brelandId, "Located in"));

    var postgres = await AssertRejectedAsync(Link(settingId, sharnId, brelandId, "LOCATED IN"));

    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
  }

  [Fact]
  public async Task Description_is_limited_to_1000_characters()
  {
    var (settingId, sharnId, brelandId) = await CreateWorldAsync();
    await AddAsync(Link(settingId, sharnId, brelandId, description: new string('d', 1000)));

    var postgres = await AssertRejectedAsync(
        Link(settingId, brelandId, sharnId, description: new string('d', 1001)));

    Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, postgres.SqlState);
  }
}
