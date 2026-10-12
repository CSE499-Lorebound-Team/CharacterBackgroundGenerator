namespace Lorebound.Api.Models;

/// <summary>
/// A code a GameMaster shares so others can join a setting. Accepting it
/// always grants <see cref="SettingRole.Player"/>. Revoking sets
/// <see cref="RevokedAt"/>; rows are kept so the GM can see past invites.
/// </summary>
public class SettingInvite : ICreatedAt
{
  public Guid Id { get; set; }

  public Guid CampaignSettingId { get; set; }

  public CampaignSetting CampaignSetting { get; set; } = null!;

  /// <summary>10 Crockford base32 characters from <see cref="Sharing.InviteCodes"/>; unique.</summary>
  public string Code { get; set; } = string.Empty;

  public Guid CreatedByUserId { get; set; }

  public ApplicationUser CreatedBy { get; set; } = null!;

  /// <summary>Null means the invite never expires.</summary>
  public DateTimeOffset? ExpiresAt { get; set; }

  /// <summary>Null means unlimited uses.</summary>
  public int? MaxUses { get; set; }

  public int UseCount { get; set; }

  public DateTimeOffset? RevokedAt { get; set; }

  public DateTimeOffset CreatedAt { get; set; }
}
