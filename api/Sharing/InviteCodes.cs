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
}
