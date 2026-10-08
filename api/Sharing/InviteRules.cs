using System.Linq.Expressions;
using Lorebound.Api.Models;

namespace Lorebound.Api.Sharing;

/// <summary>Where an invite stands at a given moment. Not stored; computed.</summary>
public enum InviteStatus
{
  Active,
  Expired,
  Revoked,
  Exhausted,
}

/// <summary>
/// The one definition of a usable invite, shared by the active-invite cap
/// (P3-02), the status shown to GMs (P3-03) and preview/accept (P3-04, P3-05).
/// </summary>
public static class InviteRules
{
  public const int DefaultExpiresInDays = 7;

  public const int MaxActivePerSetting = 20;

  /// <summary>For queries: not revoked, not expired and with uses left.</summary>
  public static Expression<Func<SettingInvite, bool>> IsActiveAt(DateTimeOffset now) =>
      invite => invite.RevokedAt == null
          && (invite.ExpiresAt == null || invite.ExpiresAt > now)
          && (invite.MaxUses == null || invite.UseCount < invite.MaxUses);

  /// <summary>
  /// Revoked wins over expired, which wins over exhausted, so a GM sees the
  /// state they caused before the one that happened on its own.
  /// </summary>
  public static InviteStatus StatusAt(this SettingInvite invite, DateTimeOffset now)
  {
    if (invite.RevokedAt is not null)
    {
      return InviteStatus.Revoked;
    }

    if (invite.ExpiresAt <= now)
    {
      return InviteStatus.Expired;
    }

    if (invite.UseCount >= invite.MaxUses)
    {
      return InviteStatus.Exhausted;
    }

    return InviteStatus.Active;
  }
}
