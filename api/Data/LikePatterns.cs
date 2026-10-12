namespace Lorebound.Api.Data;

/// <summary>Builds ILIKE patterns from user input, with wildcards escaped.</summary>
public static class LikePatterns
{
  /// <summary>Pass as the escape character of <c>EF.Functions.ILike</c>.</summary>
  public const string EscapeCharacter = "\\";

  /// <summary>Matches values containing <paramref name="value"/> literally.</summary>
  public static string Contains(string value) => $"%{Escape(value)}%";

  public static string Escape(string value) => value
      .Replace("\\", "\\\\")
      .Replace("%", "\\%")
      .Replace("_", "\\_");
}
