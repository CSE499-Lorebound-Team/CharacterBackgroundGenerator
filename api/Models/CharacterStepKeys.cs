namespace Lorebound.Api.Models;

/// <summary>
/// <see cref="CharacterChoice.StepKey"/> values: the keys of the builder
/// step catalog (<see cref="BuilderSteps"/>, P7-01). Keys are stored in
/// choice rows, so never rename one.
/// </summary>
public static class CharacterStepKeys
{
  /// <summary>The character's setting, chosen at creation; never a choice row.</summary>
  public const string Setting = "setting";

  /// <summary>Where the character comes from; shown on character lists.</summary>
  public const string Homeland = "homeland";

  public const string Culture = "culture";

  public const string Religion = "religion";

  public const string SocialClass = "social_class";

  public const string Profession = "profession";

  public const string Motivation = "motivation";

  /// <summary>Shows every answer before completing; never a choice row.</summary>
  public const string Review = "review";
}
