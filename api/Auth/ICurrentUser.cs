namespace Lorebound.Api.Auth;

/// <summary>
/// The signed-in user for the current request. Inject it (scoped) into
/// controllers and services instead of reading claims directly. Every member
/// throws <see cref="UnauthorizedAccessException"/> when nobody is signed in,
/// which the fallback policy normally prevents.
/// </summary>
public interface ICurrentUser
{
  Guid UserId { get; }

  string Email { get; }

  string DisplayName { get; }
}
