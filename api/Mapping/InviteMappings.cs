using Lorebound.Api.Dtos.Invites;
using Lorebound.Api.Models;
using Lorebound.Api.Sharing;

namespace Lorebound.Api.Mapping;

public static class InviteMappings
{
  public static InviteDto ToDto(
      this SettingInvite invite,
      string joinUrl,
      DateTimeOffset now) => new(
      invite.Id,
      invite.Code,
      joinUrl,
      invite.StatusAt(now),
      invite.ExpiresAt,
      invite.MaxUses,
      invite.UseCount,
      invite.RevokedAt,
      invite.CreatedAt);
}
