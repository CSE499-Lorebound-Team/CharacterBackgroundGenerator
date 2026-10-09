using Lorebound.Api.Dtos.Relationships;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

/// <summary>
/// The suggested relationship types (P5-05, ADR 0002). The same for every
/// setting; signed-in users only, like every endpoint not marked public.
/// </summary>
[ApiController]
[Route("api/relationship-types")]
public class RelationshipTypesController : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<RelationshipTypesDto>(StatusCodes.Status200OK)]
  public ActionResult<RelationshipTypesDto> Get() =>
      Ok(new RelationshipTypesDto(RelationshipTypes.Suggested));
}
