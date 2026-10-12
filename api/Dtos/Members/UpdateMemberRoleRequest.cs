using System.ComponentModel.DataAnnotations;
using Lorebound.Api.Models;

namespace Lorebound.Api.Dtos.Members;

public record UpdateMemberRoleRequest(
    [Required]
    [EnumDataType(typeof(SettingRole))]
    SettingRole? Role);
