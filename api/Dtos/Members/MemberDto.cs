using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Members;

/// <summary>One member of a setting. Never carries an email.</summary>
public record MemberDto(
    Guid UserId,
    string DisplayName,
    SettingRole Role,
    bool IsOwner,
    DateTimeOffset JoinedAt);
