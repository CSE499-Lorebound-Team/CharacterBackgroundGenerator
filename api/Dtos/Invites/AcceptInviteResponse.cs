using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Invites;

/// <summary>The setting the user now belongs to, and their role in it.</summary>
public record AcceptInviteResponse(
    Guid SettingId,
    SettingRole MyRole);
