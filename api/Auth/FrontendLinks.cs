using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Lorebound.Api.Auth;

public class AppOptions
{
  public const string SectionName = "App";

  /// <summary>Where emailed links point, e.g. http://localhost:3000.</summary>
  [Required, Url]
  public string FrontendBaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Builds the links sent by email. They open frontend pages, which post the
/// code back to the API (P1-06, P1-07).
/// </summary>
public class FrontendLinks
{
  private readonly IOptions<AppOptions> _options;

  public FrontendLinks(IOptions<AppOptions> options)
  {
    _options = options;
  }

  public string ConfirmEmail(Guid userId, string token) =>
      Build("confirm-email", new()
      {
        ["userId"] = userId.ToString(),
        ["code"] = EmailCodes.Encode(token),
      });

  public string ResetPassword(string email, string token) =>
      Build("reset-password", new()
      {
        ["email"] = email,
        ["code"] = EmailCodes.Encode(token),
      });

  private string Build(string page, Dictionary<string, string?> query) =>
      QueryHelpers.AddQueryString(
          $"{_options.Value.FrontendBaseUrl.TrimEnd('/')}/{page}", query);
}
