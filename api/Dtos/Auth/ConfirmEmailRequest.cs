using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Auth;

/// <summary>The userId and code from the emailed confirmation link.</summary>
public record ConfirmEmailRequest(
    [Required] Guid? UserId,
    [Required] string? Code);
