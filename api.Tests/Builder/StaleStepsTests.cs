using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Builder;

// P7-04: changing an earlier step reports later choices that no longer fit
// the narrowed options, and never deletes them.
[Collection(PostgresCollection.Name)]
public class StaleStepsTests : PostgresTestBase
{
  public StaleStepsTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static async Task<JsonElement> SaveAsync(HttpClient client, Guid characterId, string stepKey, Guid entryId)
  {
    var response = await client.PutAsJsonAsync(
        $"/api/characters/{characterId}/choices/{stepKey}", new { entryIds = new[] { entryId } });
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static async Task<JsonElement> GetAsync(HttpClient client, Guid characterId)
  {
    var response = await client.GetAsync($"/api/characters/{characterId}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> StaleSteps(JsonElement detail) =>
      detail.GetProperty("staleSteps").EnumerateArray().Select(key => key.GetString()!).ToList();

  [Fact]
  public async Task Switching_homeland_flags_the_culture_without_deleting_it()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var wroat = await world.AddEntryAsync("Wroat");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var dwarven = await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    await world.LinkAsync(brelish, sharn, "Native to");
    await world.LinkAsync(wroat, dwarven, "Home of");
    var character = await world.AddCharacterAsync(Caller.Player);
    var player = world[Caller.Player];
    await SaveAsync(player, character.Id, "homeland", sharn.Id);
    Assert.Empty(StaleSteps(await SaveAsync(player, character.Id, "culture", brelish.Id)));

    var switched = await SaveAsync(player, character.Id, "homeland", wroat.Id);

    Assert.Equal(["culture"], StaleSteps(switched));
    Assert.Equal(["culture"], StaleSteps(await GetAsync(player, character.Id)));
    var culture = switched.GetProperty("choices").EnumerateArray()
        .Single(choice => choice.GetProperty("stepKey").GetString() == "culture");
    Assert.Equal(brelish.Id, culture.GetProperty("entryId").GetGuid());

    // Choosing a fitting culture clears the flag.
    Assert.Empty(StaleSteps(await SaveAsync(player, character.Id, "culture", dwarven.Id)));
  }

  [Fact]
  public async Task Every_stale_step_is_listed_in_step_order()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var wroat = await world.AddEntryAsync("Wroat");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var dwarven = await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    var flame = await world.AddEntryAsync("Silver Flame", SettingEntryType.Religion);
    var host = await world.AddEntryAsync("Sovereign Host", SettingEntryType.Religion);
    await world.LinkAsync(brelish, sharn);
    await world.LinkAsync(flame, sharn);
    await world.LinkAsync(dwarven, wroat);
    await world.LinkAsync(host, wroat);
    var character = await world.AddCharacterAsync(Caller.Player);
    var player = world[Caller.Player];
    await SaveAsync(player, character.Id, "homeland", sharn.Id);
    await SaveAsync(player, character.Id, "culture", brelish.Id);
    await SaveAsync(player, character.Id, "religion", flame.Id);

    var switched = await SaveAsync(player, character.Id, "homeland", wroat.Id);

    Assert.Equal(["culture", "religion"], StaleSteps(switched));
    Assert.Equal(3, switched.GetProperty("choices").GetArrayLength());
  }

  [Fact]
  public async Task Without_narrowing_a_changed_homeland_leaves_nothing_stale()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var wroat = await world.AddEntryAsync("Wroat");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    await world.LinkAsync(brelish, sharn);
    var character = await world.AddCharacterAsync(Caller.Player);
    var player = world[Caller.Player];
    await SaveAsync(player, character.Id, "homeland", sharn.Id);
    await SaveAsync(player, character.Id, "culture", brelish.Id);

    // Wroat has no links, so culture options are not narrowed.
    Assert.Empty(StaleSteps(await SaveAsync(player, character.Id, "homeland", wroat.Id)));
  }

  [Fact]
  public async Task A_new_character_and_a_deleted_chosen_entry_are_not_stale()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var dwarven = await world.AddEntryAsync("Dwarven", SettingEntryType.Culture);
    await world.LinkAsync(dwarven, sharn);
    var character = await world.AddCharacterAsync(Caller.Player);
    var player = world[Caller.Player];
    Assert.Empty(StaleSteps(await GetAsync(player, character.Id)));

    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);
    await world.AddChoiceAsync(character, CharacterStepKeys.Culture, brelish);
    Assert.Equal(["culture"], StaleSteps(await GetAsync(player, character.Id)));

    await Factory.WithDbAsync(db => db.SettingEntries.Where(e => e.Id == brelish.Id).ExecuteDeleteAsync());
    Assert.Empty(StaleSteps(await GetAsync(player, character.Id)));
  }

  [Fact]
  public async Task Staleness_is_judged_by_the_owners_view_so_hidden_links_never_show()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var sharn = await world.AddEntryAsync("Sharn");
    var brelish = await world.AddEntryAsync("Brelish", SettingEntryType.Culture);
    var secret = await world.AddEntryAsync("Changeling", SettingEntryType.Culture, isGmOnly: true);
    await world.LinkAsync(secret, sharn);
    var character = await world.AddCharacterAsync(Caller.Player);
    await world.AddChoiceAsync(character, CharacterStepKeys.Homeland, sharn);
    await world.AddChoiceAsync(character, CharacterStepKeys.Culture, brelish);

    // For a GameMaster the culture step would be narrowed to Changeling;
    // the Player cannot see that link, so nothing is stale for anyone.
    Assert.Empty(StaleSteps(await GetAsync(world[Caller.Player], character.Id)));
    Assert.Empty(StaleSteps(await GetAsync(world[Caller.GameMaster], character.Id)));
  }
}
