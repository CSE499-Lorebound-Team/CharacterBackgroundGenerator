namespace Lorebound.Api.Dtos.Auth;

/// <summary>The signed-in user's summary. Never carries a token.</summary>
public record AuthUserDto(
    Guid Id,
    string Email,
    string DisplayName);
