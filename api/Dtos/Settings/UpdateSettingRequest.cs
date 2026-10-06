using System.ComponentModel.DataAnnotations;

namespace Lorebound.Api.Dtos.Settings;

public record UpdateSettingRequest(
    [Required]
    [StringLength(100, MinimumLength = 1)]
    string Name,

    [StringLength(1000)]
    string? Description);