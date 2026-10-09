using System.Net;
using System.Reflection;
using Lorebound.Api.Models;
using Lorebound.Api.Tests.TestSupport;
using Caller = Lorebound.Api.Tests.TestSupport.SharingWorld.Caller;

namespace Lorebound.Api.Tests.Builder;

// P7-01: one catalog that the frontend wizard and choice validation share.
public class BuilderStepsCatalogTests
{
  [Fact]
  public void Catalog_has_eight_steps_with_unique_keys_and_contiguous_orders()
  {
    Assert.Equal(8, BuilderSteps.All.Count);
    Assert.Equal(
        BuilderSteps.All.Count,
        BuilderSteps.All.Select(step => step.Key).Distinct(StringComparer.Ordinal).Count());
    Assert.Equal(
        Enumerable.Range(1, BuilderSteps.All.Count),
        BuilderSteps.All.Select(step => step.Order));
  }

  [Fact]
  public void Every_step_key_constant_is_in_the_catalog_and_fits_the_column()
  {
    var keys = typeof(CharacterStepKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (string)field.GetValue(null)!)
        .ToList();

    Assert.Equal(keys.Order(), BuilderSteps.All.Select(step => step.Key).Order());
    Assert.All(keys, key => Assert.InRange(key.Length, 1, CharacterChoice.StepKeyMaxLength));
  }

  [Fact]
  public void Steps_that_store_choices_allow_at_least_one_and_the_others_none()
  {
    Assert.All(BuilderSteps.All, step =>
    {
      if (step.StoresChoices)
      {
        Assert.True(step.MaxSelections >= 1, step.Key);
        Assert.False(step.EntryType is not null && step.AllowFreeText, step.Key);
      }
      else
      {
        Assert.Equal(0, step.MaxSelections);
      }
    });
  }

  [Fact]
  public void Catalog_matches_the_agreed_steps()
  {
    Assert.Collection(
        BuilderSteps.All,
        step => AssertStep(step, CharacterStepKeys.Setting, null, false, true),
        step => AssertStep(step, CharacterStepKeys.Homeland, SettingEntryType.Location, false, true),
        step => AssertStep(step, CharacterStepKeys.Culture, SettingEntryType.Culture, false, true),
        step => AssertStep(step, CharacterStepKeys.Religion, SettingEntryType.Religion, false, false),
        step => AssertStep(step, CharacterStepKeys.SocialClass, SettingEntryType.SocialClass, false, false),
        step => AssertStep(step, CharacterStepKeys.Profession, SettingEntryType.Profession, false, true),
        step => AssertStep(step, CharacterStepKeys.Motivation, null, true, true),
        step => AssertStep(step, CharacterStepKeys.Review, null, false, false));
  }

  [Fact]
  public void Find_matches_the_exact_key_only()
  {
    Assert.Equal(CharacterStepKeys.Culture, BuilderSteps.Find("culture")?.Key);
    Assert.Null(BuilderSteps.Find("Culture"));
    Assert.Null(BuilderSteps.Find("faction"));
  }

  private static void AssertStep(
      BuilderStep step, string key, SettingEntryType? entryType, bool allowFreeText, bool required)
  {
    Assert.Equal(key, step.Key);
    Assert.Equal(entryType, step.EntryType);
    Assert.Equal(allowFreeText, step.AllowFreeText);
    Assert.Equal(required, step.Required);
  }
}

[Collection(PostgresCollection.Name)]
public class BuilderStepsEndpointTests : PostgresTestBase
{
  public BuilderStepsEndpointTests(PostgresFixture fixture)
      : base(fixture)
  {
  }

  [Fact]
  public async Task Signed_in_users_get_the_ordered_catalog()
  {
    var world = await SharingWorld.SeedAsync(Factory);

    var response = await world[Caller.NonMember].GetAsync("/api/builder/steps");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await JsonAssert.HasExactlyPropertiesAsync(response, "steps");
    var steps = body.GetProperty("steps").EnumerateArray().ToList();
    Assert.Equal(BuilderSteps.All.Select(step => step.Key), steps.Select(step => step.GetProperty("key").GetString()));
    Assert.Equal(Enumerable.Range(1, 8), steps.Select(step => step.GetProperty("order").GetInt32()));

    var homeland = steps[1];
    Assert.Equal(
        ["allowFreeText", "description", "entryType", "key", "maxFreeTextLength", "maxSelections", "order", "required", "title"],
        homeland.EnumerateObject().Select(property => property.Name).Order());
    Assert.Equal("Choose Your Homeland", homeland.GetProperty("title").GetString());
    Assert.Equal("Location", homeland.GetProperty("entryType").GetString());
    Assert.Equal(System.Text.Json.JsonValueKind.Null, homeland.GetProperty("maxFreeTextLength").ValueKind);

    var motivation = steps[6];
    Assert.Equal(System.Text.Json.JsonValueKind.Null, motivation.GetProperty("entryType").ValueKind);
    Assert.True(motivation.GetProperty("allowFreeText").GetBoolean());
    Assert.Equal(CharacterChoice.FreeTextMaxLength, motivation.GetProperty("maxFreeTextLength").GetInt32());
  }

  [Fact]
  public async Task Anonymous_gets_401()
  {
    var response = await Factory.CreateCookieClient().GetAsync("/api/builder/steps");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }
}
