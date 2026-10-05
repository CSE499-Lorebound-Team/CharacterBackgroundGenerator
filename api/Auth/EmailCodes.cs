using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Lorebound.Api.Auth;

/// <summary>
/// Makes Identity's one-time tokens (email confirmation, password reset) safe
/// to put in a URL. These are single-use link parameters, not session tokens.
/// </summary>
public static class EmailCodes
{
  public static string Encode(string token) =>
      WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

  /// <summary>False for a code that is not valid Base64Url.</summary>
  public static bool TryDecode(string code, [NotNullWhen(true)] out string? token)
  {
    try
    {
      token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
      return true;
    }
    catch (FormatException)
    {
      token = null;
      return false;
    }
  }
}
