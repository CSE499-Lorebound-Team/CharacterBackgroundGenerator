using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Lorebound.Api.Sharing;

/// <summary>
/// Invite codes: 10 characters of Crockford base32 (no I, L, O or U, so
/// codes are easy to read aloud and type), about 50 bits of randomness.
/// They are stored in plain text so a GM can view and re-share them; being
/// unguessable and revocable is what keeps them safe.
/// </summary>
public static class InviteCodes
{
  public const int Length = 10;

  public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

  public static string Generate() =>
      RandomNumberGenerator.GetString(Alphabet, Length);

  /// <summary>
  /// Reads a code the way a person might type it: case-insensitive, with
  /// hyphens and spaces ignored, and I/L read as 1 and O as 0 (Crockford's
  /// rules). False when the result cannot be a code, so callers answer 404
  /// without a database lookup.
  /// </summary>
  public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? code)
  {
    code = null;
    if (input is null)
    {
      return false;
    }

    var chars = new List<char>(Length);
    foreach (var c in input.ToUpperInvariant())
    {
      if (c is '-' or ' ')
      {
        continue;
      }

      var mapped = c switch
      {
        'I' or 'L' => '1',
        'O' => '0',
        _ => c,
      };

      if (chars.Count == Length || !Alphabet.Contains(mapped))
      {
        return false;
      }

      chars.Add(mapped);
    }

    if (chars.Count != Length)
    {
      return false;
    }

    code = new string([.. chars]);
    return true;
  }
}
