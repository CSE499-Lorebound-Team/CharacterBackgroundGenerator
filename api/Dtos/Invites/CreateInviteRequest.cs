using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Invites;

/// <summary>Both optional: expiry defaults to 7 days, uses to unlimited.</summary>
public record CreateInviteRequest(
    [Range(1, 90)]
    int? ExpiresInDays,

    [Range(1, 100)]
    int? MaxUses);
