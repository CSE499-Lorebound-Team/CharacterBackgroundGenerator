using System.Net;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Builder;

// P7-02 (ADR 0002): a step's options are the setting's visible entries of
// its type, narrowed to those linked to an earlier choice when any are.
[Collection(PostgresCollection.Name)]
public class StepOptionsTests : PostgresTestBase
{
  public StepOptionsTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static string OptionsPath(Guid characterId, string stepKey) =>
      $"/api/characters/{characterId}/steps/{stepKey}/options";

  private static async Task<(bool Narrowed, string Names)> ReadOptionsAsync(HttpResponseMessage response)
  {
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    var names = body.GetProperty("options").EnumerateArray()
        .Select(option => option.GetProperty("name").GetString()!)
        .ToList();
    return (body.GetProperty("narrowed").GetBoolean(), string.Join(", ", names));
  }

  [Fact]
  public async Task A_homeland_linked_to_two_cultures_offers_only_those_in_either_direction()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture, description: "Pragmatic city folk.");
    var dwarven = await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    await world.AddEntryAsync("Elven", SettingEntryType.Culture);
    await world.LinkAsync(brelish, sharn, "Native to");
    await world.LinkAsync(sharn, dwarven, "Home of");
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);

    var response = await world[Caller.Player].GetAsync(OptionsPath(character.Id, "culture"));

    var body = await JsonAssert.HasExactlyPropertiesAsync(response, "stepKey", "narrowed", "options");
    Assert.Equal("culture", body.GetProperty("stepKey").GetString());
    Assert.True(body.GetProperty("narrowed").GetBoolean());
    var options = body.GetProperty("options").EnumerateArray().ToList();
    Assert.Equal(["Brelish", "Dwarven"], options.Select(option => option.GetProperty("name").GetString()));
    Assert.Equal(
        ["description", "entryId", "name"],
        options[0].EnumerateObject().Select(property => property.Name).Order());
    Assert.Equal(brelish.Id, options[0].GetProperty("entryId").GetGuid());
    Assert.Equal("Pragmatic city folk.", options[0].GetProperty("description").GetString());
  }

  [Fact]
  public async Task Without_a_linked_candidate_every_candidate_is_offered_not_narrowed()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    var character = await world.AddCharacterAsync(Caller.Player);

    var noChoice = await ReadOptionsAsync(await world[Caller.Player].GetAsync(OptionsPath(character.Id, "culture")));
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);
    var unlinked = await ReadOptionsAsync(await world[Caller.Player].GetAsync(OptionsPath(character.Id, "culture")));

    Assert.Equal((false, "Brelish, Dwarven"), noChoice);
    Assert.Equal((false, "Brelish, Dwarven"), unlinked);
  }

  [Fact]
  public async Task Only_the_steps_type_in_the_characters_setting_is_offered()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var other = await world.CreateSettingAsync("Eberron");
    await world.AddEntryAsync("Sharn");
    await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    await world.AddEntryAsync("Aundairian", SettingEntryType.Culture, settingId: other);
    await world.AddEntryAsync("Smith", SettingEntryType.Profession);
    var character = await world.AddCharacterAsync(Caller.Player);

    Assert.Equal((false, "Brelish"), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, "culture"))));
    Assert.Equal((false, "Sharn"), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, "homeland"))));
    Assert.Equal((false, "Smith"), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, "profession"))));
  }

  [Fact]
  public async Task Every_earlier_entry_choice_narrows_and_later_ones_do_not()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var flame = await world.AddEntryAsync("Silver Flame", SettingEntryType.Religion);
    var smith = await world.AddEntryAsync("Smith", SettingEntryType.Profession);
    var guild = await world.AddEntryAsync("Artisan", SettingEntryType.SocialClass);
    await world.AddEntryAsync("Noble", SettingEntryType.SocialClass);
    await world.AddEntryAsync("Sovereign Host", SettingEntryType.Religion);
    await world.LinkAsync(flame, brelish, "Practiced in");
    await world.LinkAsync(smith, guild, "Part of");
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);
    await world.AddChoiceAsync(character, CharacterStepKeys.Culture, brelish);
    await world.AddChoiceAsync(character, CharacterStepKeys.Profession, smith);

    // Religion (4) is narrowed by the culture (3); social class (5) is not
    // narrowed by the profession (6), which comes later.
    Assert.Equal((true, "Silver Flame"), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, "religion"))));
    Assert.Equal((false, "Artisan, Noble"), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, "social_class"))));
  }

  [Fact]
  public async Task A_player_never_gets_gm_only_entries_and_hidden_links_do_not_narrow()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var secret = await world.AddEntryAsync("Changeling", SettingEntryType.Culture, isGmOnly: true);
    await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    await world.LinkAsync(secret, sharn, "Native to");
    var playerCharacter = await world.AddCharacterAsync(Caller.Player);
    var gmCharacter = await world.AddCharacterAsync(Caller.GameMaster, "Villain");
    await world.AddChoiceAsync(playerCharacter, CharacterStepKeys.Homeland, sharn);
    await world.AddChoiceAsync(gmCharacter, CharacterStepKeys.Homeland, sharn);

    var player = await world[Caller.Player].GetAsync(OptionsPath(playerCharacter.Id, "culture"));
    var playerJson = await player.Content.ReadAsStringAsync();

    Assert.Equal((false, "Brelish"), await ReadOptionsAsync(player));
    Assert.DoesNotContain("Changeling", playerJson);
    Assert.DoesNotContain(secret.Id.ToString(), playerJson);
    Assert.Equal((true, "Changeling"), await ReadOptionsAsync(
        await world[Caller.GameMaster].GetAsync(OptionsPath(gmCharacter.Id, "culture"))));
  }

  [Fact]
  public async Task A_chosen_entry_that_became_gm_only_does_not_narrow_for_a_player()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn", isGmOnly: true);
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    await world.LinkAsync(brelish, sharn, "Native to");
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);

    Assert.Equal((false, "Brelish, Dwarven"), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, "culture"))));
  }

  [Theory]
  [InlineData("setting")]
  [InlineData("motivation")]
  [InlineData("review")]
  public async Task Steps_without_an_entry_type_have_no_options(string stepKey)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddEntryAsync("Sharn");
    var character = await world.AddCharacterAsync(Caller.Player);

    Assert.Equal((false, ""), await ReadOptionsAsync(
        await world[Caller.Player].GetAsync(OptionsPath(character.Id, stepKey))));
  }

  [Theory]
  [InlineData("faction")]
  [InlineData("Culture")]
  public async Task An_unknown_step_is_404(string stepKey)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);

    var response = await world[Caller.Player].GetAsync(OptionsPath(character.Id, stepKey));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.Equal("Builder step not found.", problem.GetProperty("detail").GetString());
  }

  [Theory]
  [InlineData(Caller.Anonymous, HttpStatusCode.Unauthorized)]
  [InlineData(Caller.NonMember, HttpStatusCode.NotFound)]
  [InlineData(Caller.GameMaster, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Owner, HttpStatusCode.Forbidden)]
  [InlineData(Caller.Player, HttpStatusCode.OK)]
  public async Task Only_the_owner_with_write_access_gets_options(Caller caller, HttpStatusCode expected)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);

    var response = await world[caller].GetAsync(OptionsPath(character.Id, "homeland"));

    Assert.Equal(expected, response.StatusCode);
  }

  [Fact]
  public async Task A_removed_owner_gets_403_and_a_missing_character_404()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.RemoveMemberAsync(Caller.Player);

    var removed = await world[Caller.Player].GetAsync(OptionsPath(character.Id, "homeland"));
    var missing = await world[Caller.Player].GetAsync(OptionsPath(Guid.NewGuid(), "homeland"));

    Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    Assert.Equal(JsonValueKind.Object, (await JsonAssert.ReadProblemAsync(missing)).ValueKind);
  }
}
