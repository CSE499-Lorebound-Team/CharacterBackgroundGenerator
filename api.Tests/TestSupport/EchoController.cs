using Lorebound.Api.Dtos.Entries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Tests.TestSupport;

[ApiController]
[AllowAnonymous]
[Route("test/echo")]
public class EchoController : ControllerBase
{
  [HttpPost("entry")]
  public SettingEntryDto Entry(SettingEntryDto dto) => dto;
}
