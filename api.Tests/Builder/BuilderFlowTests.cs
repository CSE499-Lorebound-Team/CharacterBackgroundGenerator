using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lorebound.Api.Tests.TestSupport;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Builder;

// P7-06: the guided builder end to end, only through the HTTP API: a
// GameMaster writes the lore and links it, a Player walks the steps from
// the catalog, and the character completes, goes stale, is fixed and
// reopened, and turns read-only after removal.
[Collection(PostgresCollection.Name)]
public class BuilderFlowTests : PostgresTestBase
{
  public BuilderFlowTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private sealed class Flow
  {
    public required SharingWorld World { get; init; }

    public Dictionary<string, Guid> Entries { get; } = new();

    public HttpClient Gm => World[Caller.GameMaster];

    public HttpClient Player => World[Caller.Player];
  }

  // Sharn and Wroat are homelands; Brelish is native to Sharn, Dwarven to
  // Wroat; Elven is linked to nothing; Changeling is GM-only and native to
  // Sharn. The Silver Flame is practiced by the Brelish. Plus a social
  // class and two professions, none linked.
  private async Task<Flow> SeedLoreAsync()
  {
    var flow = new Flow { World = await SharingWorld.SeedAsync(Factory) };

    foreach (var (name, type, gmOnly) in new[]
    {
      ("Sharn", "Location", false),
      ("Wroat", "Location", false),
      ("Brelish", "Culture", false),
      ("Dwarven", "Culture", false),
      ("Elven", "Culture", false),
      ("Changeling", "Culture", true),
      ("Silver Flame", "Religion", false),
      ("Artisan", "SocialClass", false),
      ("Smith", "Profession", false),
      ("Scribe", "Profession", false),
    })
    {
      var created = await flow.Gm.PostAsJsonAsync(
          $"/api/settings/{flow.World.SettingId}/entries",
          new { name, entryType = type, description = $"About {name}.", isGmOnly = gmOnly });
      Assert.Equal(HttpStatusCode.Created, created.StatusCode);
      flow.Entries[name] = (await JsonAssert.ReadJsonAsync(created)).GetProperty("id").GetGuid();
    }

    foreach (var (source, type, target) in new[]
    {
      ("Brelish", "Native to", "Sharn"),
      ("Wroat", "Home of", "Dwarven"),
      ("Changeling", "Native to", "Sharn"),
      ("Silver Flame", "Practiced in", "Brelish"),
    })
    {
      var linked = await flow.Gm.PostAsJsonAsync(
          $"/api/settings/{flow.World.SettingId}/relationships",
          new { sourceEntryId = flow.Entries[source], targetEntryId = flow.Entries[target], relationshipType = type });
      Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
    }

    return flow;
  }

  private static async Task<Guid> CreateCharacterAsync(Flow flow)
  {
    var created = await flow.Player.PostAsJsonAsync(
        "/api/characters", new { settingId = flow.World.SettingId, name = "Aster Vane" });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    return (await JsonAssert.ReadJsonAsync(created)).GetProperty("id").GetGuid();
  }

  private static async Task<(bool Narrowed, List<string> Names)> OptionsAsync(Flow flow, Guid id, string stepKey)
  {
    var response = await flow.Player.GetAsync($"/api/characters/{id}/steps/{stepKey}/options");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.ReadJsonAsync(response);
    return (
        body.GetProperty("narrowed").GetBoolean(),
        body.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("name").GetString()!).ToList());
  }

  private static Task<HttpResponseMessage> ChooseAsync(Flow flow, Guid id, string stepKey, params string[] names) =>
      flow.Player.PutAsJsonAsync(
          $"/api/characters/{id}/choices/{stepKey}",
          new { entryIds = names.Select(name => flow.Entries[name]).ToArray() });

  private static async Task<JsonElement> ChooseOkAsync(Flow flow, Guid id, string stepKey, params string[] names)
  {
    var response = await ChooseAsync(flow, id, stepKey, names);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static async Task<JsonElement> DetailAsync(Flow flow, Guid id)
  {
    var response = await flow.Player.GetAsync($"/api/characters/{id}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return await JsonAssert.ReadJsonAsync(response);
  }

  private static List<string> StaleSteps(JsonElement detail) =>
      detail.GetProperty("staleSteps").EnumerateArray().Select(key => key.GetString()!).ToList();

  [Fact]
  public async Task A_player_walks_every_step_from_the_catalog_and_completes()
  {
    var flow = await SeedLoreAsync();
    var id = await CreateCharacterAsync(flow);

    var steps = (await JsonAssert.ReadJsonAsync(await flow.Player.GetAsync("/api/builder/steps")))
        .GetProperty("steps").EnumerateArray().ToList();
    Assert.Equal(8, steps.Count);

    var expected = new Dictionary<string, (bool Narrowed, string[] Names)>
    {
      ["homeland"] = (false, ["Sharn", "Wroat"]),
      ["culture"] = (true, ["Brelish"]), // Changeling is linked too, but hidden from Players.
      ["religion"] = (true, ["Silver Flame"]),
      ["social_class"] = (false, ["Artisan"]),
      ["profession"] = (false, ["Scribe", "Smith"]),
    };

    foreach (var step in steps)
    {
      var key = step.GetProperty("key").GetString()!;
      if (step.GetProperty("entryType").ValueKind == JsonValueKind.String)
      {
        var (narrowed, names) = await OptionsAsync(flow, id, key);
        Assert.Equal(expected[key].Narrowed, narrowed);
        Assert.Equal(expected[key].Names, names);

        var saved = await ChooseOkAsync(flow, id, key, names[0]);
        Assert.Equal(step.GetProperty("order").GetInt32() + 1, saved.GetProperty("currentStep").GetInt32());
      }
      else if (step.GetProperty("allowFreeText").GetBoolean())
      {
        var saved = await flow.Player.PutAsJsonAsync(
            $"/api/characters/{id}/choices/{key}", new { freeText = "Find the mentor who vanished." });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
      }
    }

    var completed = await flow.Player.PostAsync($"/api/characters/{id}/complete", null);
    Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    var detail = await JsonAssert.ReadJsonAsync(completed);
    Assert.Equal("Complete", detail.GetProperty("status").GetString());
    Assert.Empty(StaleSteps(detail));
    Assert.Equal(6, detail.GetProperty("choices").GetArrayLength());

    var list = await JsonAssert.ReadJsonAsync(await flow.Player.GetAsync("/api/characters?status=Complete"));
    var item = Assert.Single(list.GetProperty("items").EnumerateArray());
    Assert.Equal("Sharn", item.GetProperty("homelandName").GetString());

    var gmView = await JsonAssert.ReadJsonAsync(
        await flow.Gm.GetAsync($"/api/settings/{flow.World.SettingId}/characters?status=Complete"));
    Assert.Single(gmView.GetProperty("items").EnumerateArray());
  }

  [Fact]
  public async Task Narrowing_falls_back_to_every_candidate_when_nothing_is_linked()
  {
    var flow = await SeedLoreAsync();
    var id = await CreateCharacterAsync(flow);

    // No homeland yet: nothing narrows the cultures.
    var (narrowed, names) = await OptionsAsync(flow, id, "culture");
    Assert.False(narrowed);
    Assert.Equal(["Brelish", "Dwarven", "Elven"], names);

    await ChooseOkAsync(flow, id, "homeland", "Wroat");
    (narrowed, names) = await OptionsAsync(flow, id, "culture");
    Assert.True(narrowed);
    Assert.Equal(["Dwarven"], names);

    // Elven links to nothing, yet the Player may still pick any culture.
    await ChooseOkAsync(flow, id, "culture", "Elven");
    Assert.Equal(["culture"], StaleSteps(await DetailAsync(flow, id)));
  }

  [Fact]
  public async Task Invalid_answers_are_rejected_and_leave_the_character_unchanged()
  {
    var flow = await SeedLoreAsync();
    var id = await CreateCharacterAsync(flow);
    var otherSetting = await flow.World.CreateSettingAsync("Eberron");
    var foreign = await flow.World[Caller.Owner].PostAsJsonAsync(
        $"/api/settings/{otherSetting}/entries", new { name = "Thrane", entryType = "Location" });
    var foreignId = (await JsonAssert.ReadJsonAsync(foreign)).GetProperty("id").GetGuid();
    var before = (await DetailAsync(flow, id)).ToString();

    var wrongType = await ChooseAsync(flow, id, "homeland", "Brelish");
    var crossSetting = await flow.Player.PutAsJsonAsync(
        $"/api/characters/{id}/choices/homeland", new { entryIds = new[] { foreignId } });
    var hidden = await ChooseAsync(flow, id, "culture", "Changeling");
    var tooMany = await ChooseAsync(flow, id, "homeland", "Sharn", "Wroat");

    foreach (var response in new[] { wrongType, crossSetting, hidden, tooMany })
    {
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Equal(JsonValueKind.Object, (await JsonAssert.ReadProblemAsync(response)).GetProperty("errors").ValueKind);
    }

    Assert.Equal(before, (await DetailAsync(flow, id)).ToString());
  }

  [Fact]
  public async Task Changing_the_homeland_strands_the_culture_until_it_is_fixed_then_it_completes_and_reopens()
  {
    var flow = await SeedLoreAsync();
    var id = await CreateCharacterAsync(flow);
    await ChooseOkAsync(flow, id, "homeland", "Sharn");
    await ChooseOkAsync(flow, id, "culture", "Brelish");
    await ChooseOkAsync(flow, id, "profession", "Smith");
    await flow.Player.PutAsJsonAsync($"/api/characters/{id}/choices/motivation", new { freeText = "Gold." });

    var switched = await ChooseOkAsync(flow, id, "homeland", "Wroat");
    Assert.Equal(["culture"], StaleSteps(switched));
    Assert.Contains(switched.GetProperty("choices").EnumerateArray(),
        choice => choice.GetProperty("entryName").GetString() == "Brelish");

    var refused = await flow.Player.PostAsync($"/api/characters/{id}/complete", null);
    Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    Assert.True((await JsonAssert.ReadProblemAsync(refused)).GetProperty("errors").TryGetProperty("culture", out _));

    Assert.Empty(StaleSteps(await ChooseOkAsync(flow, id, "culture", "Dwarven")));
    Assert.Equal(HttpStatusCode.OK, (await flow.Player.PostAsync($"/api/characters/{id}/complete", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await ChooseAsync(flow, id, "profession", "Scribe")).StatusCode);

    Assert.Equal(HttpStatusCode.OK, (await flow.Player.PostAsync($"/api/characters/{id}/reopen", null)).StatusCode);
    var edited = await ChooseOkAsync(flow, id, "profession", "Scribe");
    Assert.Equal("Draft", edited.GetProperty("status").GetString());
  }

  [Fact]
  public async Task A_removed_player_can_read_but_no_longer_build()
  {
    var flow = await SeedLoreAsync();
    var id = await CreateCharacterAsync(flow);
    await ChooseOkAsync(flow, id, "homeland", "Sharn");

    var removed = await flow.Gm.DeleteAsync(
        $"/api/settings/{flow.World.SettingId}/members/{flow.World.UserIds[Caller.Player]}");
    Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

    Assert.Equal(HttpStatusCode.Forbidden, (await ChooseAsync(flow, id, "homeland", "Wroat")).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden,
        (await flow.Player.GetAsync($"/api/characters/{id}/steps/culture/options")).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden,
        (await flow.Player.PostAsync($"/api/characters/{id}/complete", null)).StatusCode);

    var detail = await DetailAsync(flow, id);
    Assert.True(detail.GetProperty("isReadOnly").GetBoolean());
    Assert.Equal("Sharn", Assert.Single(detail.GetProperty("choices").EnumerateArray()).GetProperty("entryName").GetString());
  }
}
