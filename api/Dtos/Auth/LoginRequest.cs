using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Auth;

/// <summary>
/// RememberMe = false gives a session cookie; true gives the 14-day
/// persistent cookie.
/// </summary>
public record LoginRequest(
    [Required] string? Email,
    [Required] string? Password,
    bool RememberMe = false);
