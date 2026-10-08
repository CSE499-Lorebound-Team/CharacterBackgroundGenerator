namespace Lorebound.Api.Dtos.Invites;

/// <summary>What a signed-in user sees before accepting an invite.</summary>
public record InvitePreviewDto(
    string SettingName,
    string GmDisplayName,
    bool AlreadyMember);
