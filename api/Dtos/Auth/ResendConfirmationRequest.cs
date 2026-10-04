using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Auth;

public record ResendConfirmationRequest(
    [Required, EmailAddress] string? Email);
