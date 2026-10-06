namespace Lorebound.Api.Dtos.Auth;

public record RegisteredUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed);
