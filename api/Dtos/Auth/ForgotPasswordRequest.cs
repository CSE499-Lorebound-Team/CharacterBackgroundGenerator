using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Auth;

public record ForgotPasswordRequest(
    [Required, EmailAddress] string? Email);
