using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Auth;

/// <summary>
/// Email and code come from the emailed reset link. Identity enforces the
/// password policy on NewPassword.
/// </summary>
public record ResetPasswordRequest(
    [Required] string? Email,
    [Required] string? Code,
    [Required] string? NewPassword);
