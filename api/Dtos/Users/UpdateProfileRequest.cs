using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Users;

/// <summary>Email and password changes are out of scope for now.</summary>
public record UpdateProfileRequest(
    [Required, StringLength(60)] string? DisplayName);
