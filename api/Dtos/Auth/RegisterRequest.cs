using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Auth;

/// <summary>
/// The password policy (length 10+) is enforced by Identity, not attributes,
/// so its errors come back under "Password" like any other validation error.
/// </summary>
public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string? Email,
    [Required] string? Password,
    [Required, StringLength(60)] string? DisplayName);
