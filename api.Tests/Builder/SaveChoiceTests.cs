using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Builder;

// P7-03: validated, atomic saving of one step's answer.
[Collection(PostgresCollection.Name)]
public class SaveChoiceTests : PostgresTestBase
{
  public SaveChoiceTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string ChoicePath(Guid characterId, string stepKey) =>
      $"/api/characters/{characterId}/choices/{stepKey}";

  private Task<(int CurrentStep, DateTimeOffset UpdatedAt, List<(string StepKey, int Ordinal, Guid? EntryId, string? FreeText)> Choices)> SnapshotAsync(Guid id) =>
      Factory.WithDbAsync(async db =>
      {
        var character = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == id);
        var choices = await db.CharacterChoices.AsNoTracking()
            .Where(choice => choice.CharacterId == id)
            .OrderBy(choice => choice.StepKey).ThenBy(choice => choice.Ordinal)
            .Select(choice => new { choice.StepKey, choice.Ordinal, choice.EntryId, choice.FreeText })
            .ToListAsync();
        return (character.CurrentStep, character.UpdatedAt,
            choices.Select(c => (c.StepKey, c.Ordinal, c.EntryId, c.FreeText)).ToList());
      });

  private static void AssertUnchanged(
      (int CurrentStep, DateTimeOffset UpdatedAt, List<(string, int, Guid?, string?)> Choices) before,
      (int CurrentStep, DateTimeOffset UpdatedAt, List<(string, int, Guid?, string?)> Choices) after)
  {
    Assert.Equal(before.CurrentStep, after.CurrentStep);
    Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    Assert.Equal(before.Choices, after.Choices);
  }

  private static async Task<JsonElement> AssertBadRequestAsync(HttpResponseMessage response, string field, string message)
  {
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    var errors = problem.GetProperty("errors");
    Assert.True(errors.TryGetProperty(field, out var messages), $"No error keyed {field}: {errors}");
    Assert.Contains(message, messages.EnumerateArray().Select(m => m.GetString()));
    return errors;
  }

  [Fact]
  public async Task Saving_an_entry_stores_it_advances_the_step_and_returns_the_character()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player);
    var before = await SnapshotAsync(character.Id);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "homeland"), new { entryIds = new[] { sharn.Id } });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    Assert.Equal(character.Id, body.GetProperty("id").GetGuid());
    Assert.Equal(3, body.GetProperty("currentStep").GetInt32());
    var choice = Assert.Single(body.GetProperty("choices").EnumerateArray());
    Assert.Equal("homeland", choice.GetProperty("stepKey").GetString());
    Assert.Equal(sharn.Id, choice.GetProperty("entryId").GetGuid());
    Assert.Equal("Sharn", choice.GetProperty("entryName").GetString());
    Assert.Equal("Location", choice.GetProperty("entryType").GetString());

    var after = await SnapshotAsync(character.Id);
    Assert.Equal([("homeland", 0, (Guid?)sharn.Id, (string?)null)], after.Choices);
    Assert.True(after.UpdatedAt > before.UpdatedAt);
  }

  [Fact]
  public async Task Saving_again_replaces_the_steps_answer_and_leaves_other_steps()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var dwarven = await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);
    await world.AddChoiceAsync(character, CharacterStepKeys.Culture, brelish);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "culture"), new { entryIds = new[] { dwarven.Id } });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(
        [("culture", 0, (Guid?)dwarven.Id, (string?)null), ("homeland", 0, sharn.Id, null)],
        (await SnapshotAsync(character.Id)).Choices);
  }

  [Fact]
  public async Task Free_text_is_trimmed_and_stored_for_a_free_text_step()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);
    var longest = new string('x', CharacterChoice.FreeTextMaxLength);

    var first = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "motivation"), new { freeText = "  Avenge my mentor.  " });
    var second = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "motivation"), new { freeText = longest });

    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    Assert.Equal("Avenge my mentor.", Assert.Single((await JsonAssert.ReadJsonAsync(first))
        .GetProperty("choices").EnumerateArray()).GetProperty("freeText").GetString());
    Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    var snapshot = await SnapshotAsync(character.Id);
    Assert.Equal([("motivation", 0, (Guid?)null, (string?)longest)], snapshot.Choices);
    Assert.Equal(8, snapshot.CurrentStep);
  }

  [Fact]
  public async Task Sending_nothing_clears_the_step()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var smith = await world.AddEntryAsync("Smith", SettingEntryType.Profession);
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Profession, smith);
    await world.AddChoiceAsync(character, CharacterStepKeys.Motivation, freeText: "Gold.");

    var entries = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "profession"), new { entryIds = Array.Empty<Guid>() });
    var text = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "motivation"), new { freeText = "   " });

    Assert.Equal(HttpStatusCode.OK, entries.StatusCode);
    Assert.Equal(HttpStatusCode.OK, text.StatusCode);
    Assert.Empty((await SnapshotAsync(character.Id)).Choices);
  }

  [Fact]
  public async Task Current_step_moves_forward_only()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player, configure: c => c.CurrentStep = 6);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "homeland"), new { entryIds = new[] { sharn.Id } });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(6, (await SnapshotAsync(character.Id)).CurrentStep);
  }

  [Fact]
  public async Task An_entry_of_the_wrong_type_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var character = await world.AddCharacterAsync(Caller.Player);
    var before = await SnapshotAsync(character.Id);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "homeland"), new { entryIds = new[] { brelish.Id } });

    await AssertBadRequestAsync(response, "EntryIds[0]", "This step takes a Location entry, not a Culture.");
    AssertUnchanged(before, await SnapshotAsync(character.Id));
  }

  [Fact]
  public async Task Cross_setting_hidden_and_missing_entries_get_the_identical_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var other = await world.CreateSettingAsync("Eberron");
    var foreign = await world.AddEntryAsync("Thrane", settingId: other);
    var hidden = await world.AddEntryAsync("Mournland", isGmOnly: true);
    var character = await world.AddCharacterAsync(Caller.Player);
    var before = await SnapshotAsync(character.Id);

    var errors = new List<string>();
    foreach (var id in new[] { foreign.Id, hidden.Id, Guid.NewGuid() })
    {
      var response = await world[Caller.Player].PutAsJsonAsync(
          ChoicePath(character.Id, "homeland"), new { entryIds = new[] { id } });
      errors.Add((await AssertBadRequestAsync(response, "EntryIds[0]", "Entry not found in this setting.")).ToString());
    }

    Assert.Single(errors.Distinct());
    AssertUnchanged(before, await SnapshotAsync(character.Id));
  }

  [Fact]
  public async Task A_gameMaster_may_choose_a_gm_only_entry_for_their_own_character()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var hidden = await world.AddEntryAsync("Mournland", isGmOnly: true);
    var character = await world.AddCharacterAsync(Caller.GameMaster, "Villain");

    var response = await world[Caller.GameMaster].PutAsJsonAsync(
        ChoicePath(character.Id, "homeland"), new { entryIds = new[] { hidden.Id } });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task A_player_may_keep_an_entry_that_became_gm_only_after_choosing_it()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn", isGmOnly: true);
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "homeland"), new { entryIds = new[] { sharn.Id } });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal([("homeland", 0, (Guid?)sharn.Id, (string?)null)], (await SnapshotAsync(character.Id)).Choices);
  }

  [Fact]
  public async Task Too_many_selections_is_400()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var wroat = await world.AddEntryAsync("Wroat");
    var character = await world.AddCharacterAsync(Caller.Player);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "homeland"), new { entryIds = new[] { sharn.Id, wroat.Id } });

    await AssertBadRequestAsync(response, "EntryIds", "This step takes at most 1 entry.");
    Assert.Empty((await SnapshotAsync(character.Id)).Choices);
  }

  [Fact]
  public async Task Free_text_and_entries_are_only_accepted_where_the_step_allows_them()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player);
    var tooLong = new string('x', CharacterChoice.FreeTextMaxLength + 1);

    await AssertBadRequestAsync(
        await world[Caller.Player].PutAsJsonAsync(ChoicePath(character.Id, "homeland"), new { freeText = "Somewhere" }),
        "FreeText", "This step takes entries, not free text.");
    await AssertBadRequestAsync(
        await world[Caller.Player].PutAsJsonAsync(ChoicePath(character.Id, "motivation"), new { entryIds = new[] { sharn.Id } }),
        "EntryIds", "This step takes free text, not entries.");
    await AssertBadRequestAsync(
        await world[Caller.Player].PutAsJsonAsync(ChoicePath(character.Id, "motivation"), new { freeText = tooLong }),
        "FreeText", "The text can be at most 2000 characters.");
    Assert.Empty((await SnapshotAsync(character.Id)).Choices);
  }

  [Theory]
  [InlineData("setting")]
  [InlineData("review")]
  public async Task Steps_that_store_nothing_are_400(string stepKey)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);

    var response = await world[Caller.Player].PutAsJsonAsync(ChoicePath(character.Id, stepKey), new { });

    await AssertBadRequestAsync(response, "stepKey", "This step does not take answers.");
  }

  [Fact]
  public async Task An_unknown_step_is_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);

    var response = await world[Caller.Player].PutAsJsonAsync(
        ChoicePath(character.Id, "faction"), new { freeText = "Guild" });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("Builder step not found.",
        (await JsonAssert.ReadProblemAsync(response)).GetProperty("detail").GetString());
  }

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.GameMaster, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Owner, HttpStatusCode.Forbidden)]
  public async Task Only_the_owner_with_write_access_saves_and_refusals_change_nothing(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);
    var before = await SnapshotAsync(character.Id);

    var response = await world[caller].PutAsJsonAsync(
        ChoicePath(character.Id, "motivation"), new { freeText = "Hijacked." });

    Assert.Equal(expected, response.StatusCode);
    AssertUnchanged(before, await SnapshotAsync(character.Id));
  }
}
