using Lorebound.Api.Dtos.Builder;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The builder step catalog (P7-01). The same for every setting and
/// character; signed-in users only, like every endpoint not marked public.
/// </summary>
[ApiController]
[Route("api/builder")]
public class BuilderController : ControllerBase
{
  [HttpGet("steps")]
  [ProducesResponseType<BuilderStepsDto>(StatusCodes.Status200OK)]
  public ActionResult<BuilderStepsDto> GetSteps() =>
      Ok(new BuilderStepsDto(BuilderSteps.All.Select(BuilderStepDto.From).ToList()));
}
