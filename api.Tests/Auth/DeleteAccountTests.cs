using System.Net;
using System.Net.Http.Json;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Auth;

// P6-11: DELETE /api/users/me, blocked while owning a shared setting.
[Collection(PostgresCollection.Name)]
public class DeleteAccountTests : PostgresTestBase
{
  public DeleteAccountTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  private static Task<HttpResponseMessage> DeleteMeAsync(
      HttpClient client,
      string? password = CustomWebApplicationFactory.DefaultPassword) =>
      client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
      {
        Content = JsonContent.Create(new { password }),
      });

  private Task<bool> UserExistsAsync(Guid id) =>
      Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == id));

  private Task<(int Users, int Settings, int Memberships, int Characters, int Invites)> CountsAsync() =>
      Factory.WithDbAsync(async db => (
          await db.Users.CountAsync(),
          await db.CampaignSettings.CountAsync(),
          await db.SettingMemberships.CountAsync(),
          await db.Characters.CountAsync(),
          await db.SettingInvites.CountAsync()));

  [Theory]
  [InlineData("not the password")]
  [InlineData(null)]
  public async Task A_wrong_or_missing_password_is_400_and_deletes_nothing(string? password)
  {
    var world = await SharingWorld.SeedAsync(Factory);
    await world.AddCharacterAsync(Caller.Player);
    var before = await CountsAsync();

    var response = await DeleteMeAsync(world[Caller.Player], password);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    Assert.True(problem.GetProperty("errors").TryGetProperty("Password", out _));
    Assert.Equal(before, await CountsAsync());
    Assert.Equal(HttpStatusCode.OK, (await world[Caller.Player].GetAsync("/api/users/me")).StatusCode);
  }

  [Fact]
  public async Task Anonymous_is_401()
  {
    var response = await DeleteMeAsync(Factory.CreateCookieClient());

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Owning_a_setting_with_other_members_is_409_listing_it_and_deletes_nothing()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    // A second owned setting with no other members is not listed, and is
    // not deleted either, since the whole request is refused.
    var soloId = await world.CreateSettingAsync("Solo");
    await world.AddCharacterAsync(Caller.Owner, "Owner's");
    await world.AddInvitesAsync(1);
    var before = await CountsAsync();

    var response = await DeleteMeAsync(world[Caller.Owner]);

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var problem = await JsonAssert.ReadProblemAsync(response);
    var setting = Assert.Single(problem.GetProperty("settings").EnumerateArray());
    Assert.Equal(
        ["id", "name", "otherMemberCount"],
        setting.EnumerateObject().Select(p => p.Name));
    Assert.Equal(world.SettingId, setting.GetProperty("id").GetGuid());
    Assert.Equal("Osepia", setting.GetProperty("name").GetString());
    Assert.Equal(2, setting.GetProperty("otherMemberCount").GetInt32());
    Assert.Contains("ownership cannot be transferred", problem.GetProperty("detail").GetString());

    Assert.Equal(before, await CountsAsync());
    Assert.True(await Factory.WithDbAsync(db => db.CampaignSettings.AnyAsync(s => s.Id == soloId)));
    Assert.Equal(HttpStatusCode.OK, (await world[Caller.Owner].GetAsync("/api/users/me")).StatusCode);
  }

  [Fact]
  public async Task After_removing_every_member_the_owner_can_delete_their_account()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    foreach (var member in new[] { Caller.GameMaster, Caller.Player })
    {
      var removed = await world[Caller.Owner].DeleteAsync(
          $"/api/settings/{world.SettingId}/members/{world.UserIds[member]}");
      Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    var response = await DeleteMeAsync(world[Caller.Owner]);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(await UserExistsAsync(world.UserIds[Caller.Owner]));
    Assert.False(await Factory.WithDbAsync(db => db.CampaignSettings.AnyAsync()));
  }

  [Fact]
  public async Task Deletes_everything_the_user_owns_and_nothing_of_anyone_else()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var gmId = world.UserIds[Caller.GameMaster];
    var gm = world[Caller.GameMaster];

    // The GM's own setting, with lore, an invite and a character a removed
    // player left behind: all of it goes with the setting.
    var created = await gm.PostAsJsonAsync("/api/settings", new { name = "Mine" });
    var mineId = (await JsonAssert.ReadJsonAsync(created)).GetProperty("id").GetGuid();
    var sharn = await world.AddEntryAsync("Sharn", settingId: mineId);
    var breland = await world.AddEntryAsync("Breland", settingId: mineId);
    await world.LinkAsync(sharn, breland);
    await world.AddInvitesAsync(1, i => i.CreatedByUserId = gmId, mineId);
    var leftBehind = await world.AddCharacterAsync(Caller.NonMember, "Left behind", c => c.CampaignSettingId = mineId);
    await world.AddChoiceAsync(leftBehind, "homeland", sharn);

    // In the Owner's setting: the GM's membership, character and invites go;
    // everyone else's stay.
    var gmCharacter = await world.AddCharacterAsync(Caller.GameMaster, "GM's NPC");
    var osepiaEntry = await world.AddEntryAsync("Aundair");
    await world.AddChoiceAsync(gmCharacter, "homeland", osepiaEntry);
    await world.AddInvitesAsync(2, i => i.CreatedByUserId = gmId);
    var ownersInvite = Assert.Single(await world.AddInvitesAsync(1));
    var playersCharacter = await world.AddCharacterAsync(Caller.Player, "Player's");
    await world.AddChoiceAsync(playersCharacter, "homeland", osepiaEntry);

    var response = await DeleteMeAsync(gm);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(await UserExistsAsync(gmId));

    await Factory.WithDbAsync(async db =>
    {
      Assert.False(await db.CampaignSettings.AnyAsync(s => s.Id == mineId || s.OwnerUserId == gmId));
      Assert.False(await db.SettingEntries.AnyAsync(e => e.CampaignSettingId == mineId));
      Assert.False(await db.SettingEntryRelationships.AnyAsync(r => r.CampaignSettingId == mineId));
      Assert.False(await db.SettingMemberships.AnyAsync(m => m.UserId == gmId || m.CampaignSettingId == mineId));
      Assert.False(await db.Characters.AnyAsync(c => c.OwnerUserId == gmId || c.CampaignSettingId == mineId));
      Assert.False(await db.CharacterChoices.AnyAsync(c => c.CharacterId == gmCharacter.Id || c.CharacterId == leftBehind.Id));
      Assert.False(await db.SettingInvites.AnyAsync(i => i.CreatedByUserId == gmId || i.CampaignSettingId == mineId));

      // Everyone else is untouched.
      Assert.True(await db.CampaignSettings.AnyAsync(s => s.Id == world.SettingId));
      Assert.Equal(2, await db.SettingMemberships.CountAsync(m => m.CampaignSettingId == world.SettingId));
      Assert.Equal([playersCharacter.Id], await db.Characters.Select(c => c.Id).ToListAsync());
      Assert.Equal(1, await db.CharacterChoices.CountAsync(c => c.CharacterId == playersCharacter.Id));
      Assert.Equal([ownersInvite.Id], await db.SettingInvites.Select(i => i.Id).ToListAsync());
      Assert.True(await db.SettingEntries.AnyAsync(e => e.Id == osepiaEntry.Id));
      // Owner, Player and NonMember remain.
      Assert.Equal(3, await db.Users.CountAsync());
      return 0;
    });
  }

  [Fact]
  public async Task Signs_the_user_out_and_their_credentials_stop_working()
  {
    var world = await SharingWorld.SeedAsync(Factory);
    var otherSession = Factory.CreateCookieClient();
    var login = await otherSession.PostAsJsonAsync("/api/auth/login",
        new { email = "player@example.com", password = CustomWebApplicationFactory.DefaultPassword });
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);

    var response = await DeleteMeAsync(world[Caller.Player]);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("lorebound.auth=;"));
    Assert.Equal(HttpStatusCode.Unauthorized, (await world[Caller.Player].GetAsync("/api/users/me")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await otherSession.GetAsync("/api/users/me")).StatusCode);

    var again = await Factory.CreateCookieClient().PostAsJsonAsync("/api/auth/login",
        new { email = "player@example.com", password = CustomWebApplicationFactory.DefaultPassword });
    Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
  }

  [Fact]
  public async Task A_user_with_nothing_else_can_delete_their_account()
  {
    var (client, user) = await Factory.CreateSignedInClientAsync("solo@example.com", "Solo");

    var response = await DeleteMeAsync(client);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.False(await UserExistsAsync(user.Id));
  }
}
