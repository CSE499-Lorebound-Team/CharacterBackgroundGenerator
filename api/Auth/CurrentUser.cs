using System.Security.Claims;

namespace Lorebound.Api.Auth;

/// <summary>Reads <see cref="ICurrentUser"/> from the auth cookie's claims.</summary>
public class CurrentUser : ICurrentUser
{
  private readonly IHttpContextAccessor _httpContextAccessor;

  public CurrentUser(IHttpContextAccessor httpContextAccessor)
  {
    _httpContextAccessor = httpContextAccessor;
  }

  public Guid UserId =>
      Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
          ? id
          : throw new UnauthorizedAccessException("The signed-in principal has no user id.");

  public string Email => Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

  public string DisplayName =>
      Principal.FindFirstValue(LoreboundClaimsPrincipalFactory.DisplayNameClaimType) ?? string.Empty;

  private ClaimsPrincipal Principal
  {
    get
    {
      var principal = _httpContextAccessor.HttpContext?.User;
      if (principal?.Identity?.IsAuthenticated != true)
      {
        throw new UnauthorizedAccessException("No user is signed in.");
      }

      return principal;
    }
  }
}
