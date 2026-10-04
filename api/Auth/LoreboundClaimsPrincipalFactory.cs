using System.Security.Claims;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Lorebound.Api.Auth;

/// <summary>
/// Adds the display name to the cookie's claims (Identity already adds the
/// user id and email). The security stamp check rebuilds the claims on every
/// request, so a renamed user sees the new name straight away.
/// </summary>
public class LoreboundClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
  public const string DisplayNameClaimType = "display_name";

  public LoreboundClaimsPrincipalFactory(
      UserManager<ApplicationUser> userManager,
      IOptions<IdentityOptions> optionsAccessor)
      : base(userManager, optionsAccessor)
  {
  }

  protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
  {
    var identity = await base.GenerateClaimsAsync(user);
    identity.AddClaim(new Claim(DisplayNameClaimType, user.DisplayName));
    return identity;
  }
}
