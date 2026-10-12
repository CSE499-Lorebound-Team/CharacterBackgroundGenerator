using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Lorebound.Api.Tests.Data;

// P6-01: the Characters and CharacterChoices tables, their defaults, limits,
// constraints and delete behaviors.
[Collection(PostgresCollection.Name)]
public class CharacterTests : PostgresTestBase
{
  public CharacterTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private async Task<(Guid OwnerId, Guid SettingId, Guid EntryId)> CreateWorldAsync()
  {
    var owner = await Factory.CreateUserAsync("owner@example.com", "Owner");

    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = "Eberron",
      OwnerUserId = owner.Id,
    };
    var entry = new SettingEntry
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = setting.Id,
      Name = "Breland",
      EntryType = SettingEntryType.Location,
    };

    await Factory.WithDbAsync(db =>
    {
      db.CampaignSettings.Add(setting);
      db.SettingEntries.Add(entry);
      return db.SaveChangesAsync();
    });

    return (owner.Id, setting.Id, entry.Id);
  }

  private async Task<Guid> AddCharacterAsync(
      Guid ownerId,
      Guid settingId,
      Action<Character>? configure = null)
  {
    var character = new Character
    {
      Id = Guid.NewGuid(),
      CampaignSettingId = settingId,
      OwnerUserId = ownerId,
    };
    configure?.Invoke(character);

    await Factory.WithDbAsync(db =>
    {
      db.Characters.Add(character);
      return db.SaveChangesAsync();
    });

    return character.Id;
  }

  private static CharacterChoice Choice(
      Guid characterId,
      string stepKey = "homeland",
      int ordinal = 0,
      Guid? entryId = null,
      string? freeText = null) =>
      new()
      {
        Id = Guid.NewGuid(),
        CharacterId = characterId,
        StepKey = stepKey,
        Ordinal = ordinal,
        EntryId = entryId,
        FreeText = freeText,
      };

  private Task<int> AddAsync(params CharacterChoice[] choices) =>
      Factory.WithDbAsync(db =>
      {
        db.CharacterChoices.AddRange(choices);
        return db.SaveChangesAsync();
      });

  private static async Task<PostgresException> AssertRejectedAsync(Func<Task> save)
  {
    var error = await Assert.ThrowsAsync<DbUpdateException>(save);
    return Assert.IsType<PostgresException>(error.InnerException);
  }

  [Fact]
  public async Task A_new_character_is_an_unnamed_draft_on_step_1()
  {
    var (ownerId, settingId, _) = await CreateWorldAsync();

    var id = await AddCharacterAsync(ownerId, settingId);

    var saved = await Factory.WithDbAsync(db => db.Characters.SingleAsync(c => c.Id == id));
    Assert.Equal("Unnamed Character", saved.Name);
    Assert.Equal(CharacterStatus.Draft, saved.Status);
    Assert.Equal(1, saved.CurrentStep);
    Assert.Null(saved.Backstory);
    Assert.NotEqual(default, saved.CreatedAt);
    Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
  }

  [Fact]
  public async Task Status_is_stored_as_its_name()
  {
    var (ownerId, settingId, _) = await CreateWorldAsync();
    var id = await AddCharacterAsync(ownerId, settingId, c => c.Status = CharacterStatus.Complete);

    var stored = await Factory.WithDbAsync(db => db.Database
        .SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"Characters\" WHERE \"Id\" = {id}")
        .SingleAsync());

    Assert.Equal("Complete", stored);
  }

  [Fact]
  public async Task Name_is_limited_to_100_characters()
  {
    var (ownerId, settingId, _) = await CreateWorldAsync();
    await AddCharacterAsync(ownerId, settingId, c => c.Name = new string('n', 100));

    var postgres = await AssertRejectedAsync(
        () => AddCharacterAsync(ownerId, settingId, c => c.Name = new string('n', 101)));

    Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, postgres.SqlState);
  }

  [Fact]
  public async Task Backstory_is_limited_to_10000_characters()
  {
    var (ownerId, settingId, _) = await CreateWorldAsync();
    await AddCharacterAsync(ownerId, settingId, c => c.Backstory = new string('b', 10000));

    var postgres = await AssertRejectedAsync(
        () => AddCharacterAsync(ownerId, settingId, c => c.Backstory = new string('b', 10001)));

    Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, postgres.SqlState);
  }

  [Fact]
  public async Task A_choice_cannot_have_both_an_entry_and_free_text()
  {
    var (ownerId, settingId, entryId) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);

    var postgres = await AssertRejectedAsync(
        () => AddAsync(Choice(characterId, entryId: entryId, freeText: "Somewhere else")));

    Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
    Assert.Equal("CK_CharacterChoices_EntryOrFreeText", postgres.ConstraintName);
  }

  [Fact]
  public async Task A_choice_may_be_an_entry_or_free_text()
  {
    var (ownerId, settingId, entryId) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);

    await AddAsync(
        Choice(characterId, "homeland", entryId: entryId),
        Choice(characterId, "personality", freeText: "Curious"));

    Assert.Equal(2, await Factory.WithDbAsync(db => db.CharacterChoices.CountAsync()));
  }

  [Fact]
  public async Task The_same_step_and_ordinal_violates_the_unique_index()
  {
    var (ownerId, settingId, entryId) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);
    await AddAsync(Choice(characterId, "homeland", 0, entryId: entryId));

    var postgres = await AssertRejectedAsync(
        () => AddAsync(Choice(characterId, "homeland", 0, freeText: "Elsewhere")));

    Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    Assert.Equal("IX_CharacterChoices_CharacterId_StepKey_Ordinal", postgres.ConstraintName);
  }

  [Fact]
  public async Task Another_ordinal_or_another_character_is_not_a_duplicate()
  {
    var (ownerId, settingId, _) = await CreateWorldAsync();
    var first = await AddCharacterAsync(ownerId, settingId);
    var second = await AddCharacterAsync(ownerId, settingId);

    await AddAsync(
        Choice(first, "skills", 0, freeText: "Riding"),
        Choice(first, "skills", 1, freeText: "Forgery"),
        Choice(second, "skills", 0, freeText: "Riding"));

    Assert.Equal(3, await Factory.WithDbAsync(db => db.CharacterChoices.CountAsync()));
  }

  [Fact]
  public async Task Choice_text_columns_are_limited()
  {
    var (ownerId, settingId, _) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);
    await AddAsync(Choice(characterId, new string('s', 40), freeText: new string('f', 2000)));

    var longStep = await AssertRejectedAsync(
        () => AddAsync(Choice(characterId, new string('s', 41), freeText: "x")));
    var longText = await AssertRejectedAsync(
        () => AddAsync(Choice(characterId, "notes", freeText: new string('f', 2001))));

    Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, longStep.SqlState);
    Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, longText.SqlState);
  }

  [Fact]
  public async Task Deleting_an_entry_keeps_the_choice_with_a_null_entry()
  {
    var (ownerId, settingId, entryId) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);
    await AddAsync(Choice(characterId, "homeland", entryId: entryId));

    await Factory.WithDbAsync(db =>
        db.SettingEntries.Where(e => e.Id == entryId).ExecuteDeleteAsync());

    var choice = await Factory.WithDbAsync(db => db.CharacterChoices.SingleAsync());
    Assert.Equal(characterId, choice.CharacterId);
    Assert.Null(choice.EntryId);
    Assert.Null(choice.FreeText);
  }

  [Fact]
  public async Task Deleting_a_character_deletes_its_choices()
  {
    var (ownerId, settingId, entryId) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);
    await AddAsync(
        Choice(characterId, "homeland", entryId: entryId),
        Choice(characterId, "personality", freeText: "Curious"));

    await Factory.WithDbAsync(db =>
        db.Characters.Where(c => c.Id == characterId).ExecuteDeleteAsync());

    Assert.False(await Factory.WithDbAsync(db => db.CharacterChoices.AnyAsync()));
    Assert.True(await Factory.WithDbAsync(db => db.SettingEntries.AnyAsync(e => e.Id == entryId)));
  }

  // The entry's SetNull and the character's cascade both reach the same
  // choice; the delete must succeed whichever runs first.
  [Fact]
  public async Task Deleting_a_setting_deletes_its_characters_and_their_choices()
  {
    var (ownerId, settingId, entryId) = await CreateWorldAsync();
    var characterId = await AddCharacterAsync(ownerId, settingId);
    await AddAsync(
        Choice(characterId, "homeland", entryId: entryId),
        Choice(characterId, "personality", freeText: "Curious"));

    await Factory.WithDbAsync(db =>
        db.CampaignSettings.Where(s => s.Id == settingId).ExecuteDeleteAsync());

    Assert.False(await Factory.WithDbAsync(db => db.Characters.AnyAsync()));
    Assert.False(await Factory.WithDbAsync(db => db.CharacterChoices.AnyAsync()));
    Assert.True(await Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == ownerId)));
  }

  [Fact]
  public async Task A_user_who_owns_characters_cannot_be_deleted()
  {
    var (_, settingId, _) = await CreateWorldAsync();
    var player = await Factory.CreateUserAsync("player@example.com", "Player");
    await AddCharacterAsync(player.Id, settingId);

    var error = await Assert.ThrowsAsync<PostgresException>(() =>
        Factory.WithDbAsync(db => db.Users.Where(u => u.Id == player.Id).ExecuteDeleteAsync()));

    Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    Assert.Equal("FK_Characters_AspNetUsers_OwnerUserId", error.ConstraintName);
  }
}
