using System.Text.RegularExpressions;

namespace Lorebound.Api.Models;

/// <summary>
/// The relationship-type vocabulary (P5-05, ADR 0002): a suggested list plus
/// free text. Types are readable labels, stored as typed after
/// <see cref="Normalize"/> and compared ignoring case (the column is
/// <c>citext</c>). The builder narrows by any relationship, whatever its
/// type, so these suggestions only keep GMs' wording consistent.
/// </summary>
public static partial class RelationshipTypes
{
  public const int MaxLength = 60;

  /// <summary>Offered by <c>GET /api/relationship-types</c>, in this order.</summary>
  public static IReadOnlyList<string> Suggested { get; } =
  [
    "Located in",
    "Native to",
    "Practiced in",
    "Member of",
    "Part of",
    "Capital of",
    "Ruled by",
    "Founded by",
    "Worships",
    "Allied with",
    "Enemy of",
    "Borders",
  ];

  /// <summary>
  /// Trims, collapses inner runs of whitespace to one space and, when the
  /// result matches a suggested type ignoring case, uses its spelling, so
  /// "located  IN" is stored as "Located in".
  /// </summary>
  public static string Normalize(string relationshipType)
  {
    var collapsed = Whitespace().Replace(relationshipType.Trim(), " ");

    return Suggested.FirstOrDefault(suggested =>
            string.Equals(suggested, collapsed, StringComparison.OrdinalIgnoreCase))
        ?? collapsed;
  }

  [GeneratedRegex(@"\s+")]
  private static partial Regex Whitespace();
}
