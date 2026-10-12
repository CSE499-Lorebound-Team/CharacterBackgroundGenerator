namespace Lorebound.Api.Dtos.Users;

public record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt);
