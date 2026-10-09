using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Users;

/// <summary>Deleting the account needs the current password again (P6-11).</summary>
public record DeleteAccountRequest(
    [Required] string? Password);

/// <summary>
/// A setting that blocks account deletion: the user owns it and
/// <see cref="OtherMemberCount"/> other people still belong to it.
/// </summary>
public record BlockingSettingDto(
    Guid Id,
    string Name,
    int OtherMemberCount);
