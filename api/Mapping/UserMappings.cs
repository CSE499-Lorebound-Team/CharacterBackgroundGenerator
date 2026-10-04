using Lorebound.Api.Dtos.Auth;
using Lorebound.Api.Models;

namespace Lorebound.Api.Mapping;

public static class UserMappings
{
  public static RegisteredUserDto ToRegisteredUserDto(this ApplicationUser user) => new(
      user.Id,
      user.Email!,
      user.DisplayName,
      user.EmailConfirmed);
}
