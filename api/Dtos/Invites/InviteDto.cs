using Lorebound.Api.Sharing;

namespace Lorebound.Api.Dtos.Invites;

public record InviteDto(
    Guid Id,
    string Code,
    string JoinUrl,
    InviteStatus Status,
    DateTimeOffset? ExpiresAt,
    int? MaxUses,
    int UseCount,
    DateTimeOffset? RevokedAt,
    DateTimeOffset CreatedAt);
