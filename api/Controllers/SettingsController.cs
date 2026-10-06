using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Settings;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lorebound.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
  private readonly LoreboundDbContext _db;
  private readonly ICurrentUser _currentUser;

  public SettingsController(
      LoreboundDbContext db,
      ICurrentUser currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  [HttpPost]
  [ProducesResponseType<SettingDetailDto>(
      StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<ActionResult<SettingDetailDto>> Create(
      CreateSettingRequest request)
  {
    var setting = new CampaignSetting
    {
      Id = Guid.NewGuid(),
      Name = request.Name.Trim(),
      Description =
          NormalizeDescription(
              request.Description),
      OwnerUserId =
          _currentUser.UserId,
    };

    _db.CampaignSettings.Add(setting);

    await _db.SaveChangesAsync();

    var dto = ToDetailDto(
        setting,
        entryCount: 0);

    return CreatedAtAction(
        nameof(GetById),
        new { id = setting.Id },
        dto);
  }

  [HttpGet]
  [ProducesResponseType<IEnumerable<SettingListItemDto>>(
      StatusCodes.Status200OK)]
  public async Task<ActionResult<IEnumerable<SettingListItemDto>>> GetAll()
  {
    var settings =
        await _db.CampaignSettings
            .AsNoTracking()
            .Where(setting =>
                setting.OwnerUserId ==
                _currentUser.UserId)
            .OrderByDescending(setting =>
                setting.UpdatedAt)
            .Select(setting =>
                new SettingListItemDto(
                    setting.Id,
                    setting.Name,
                    setting.Description,
                    setting.Entries.Count,
                    setting.UpdatedAt,
                    "GM",
                    true))
            .ToListAsync();

    return Ok(settings);
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType<SettingDetailDto>(
      StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<SettingDetailDto>> GetById(
      Guid id)
  {
    var setting =
        await _db.CampaignSettings
            .AsNoTracking()
            .Where(setting =>
                setting.Id == id &&
                setting.OwnerUserId ==
                _currentUser.UserId)
            .Select(setting =>
                new SettingDetailDto(
                    setting.Id,
                    setting.Name,
                    setting.Description,
                    setting.Entries.Count,
                    setting.CreatedAt,
                    setting.UpdatedAt,
                    "GM",
                    true))
            .SingleOrDefaultAsync();

    if (setting is null)
    {
      return NotFound();
    }

    return Ok(setting);
  }

  [HttpPut("{id:guid}")]
  [ProducesResponseType<SettingDetailDto>(
      StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<SettingDetailDto>> Update(
      Guid id,
      UpdateSettingRequest request)
  {
    var setting =
        await _db.CampaignSettings
            .SingleOrDefaultAsync(setting =>
                setting.Id == id &&
                setting.OwnerUserId ==
                _currentUser.UserId);

    if (setting is null)
    {
      return NotFound();
    }

    setting.Name =
        request.Name.Trim();

    setting.Description =
        NormalizeDescription(
            request.Description);

    await _db.SaveChangesAsync();

    var entryCount =
        await _db.SettingEntries
            .CountAsync(entry =>
                entry.CampaignSettingId ==
                setting.Id);

    return Ok(
        ToDetailDto(
            setting,
            entryCount));
  }

  [HttpDelete("{id:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(
      Guid id)
  {
    var setting =
        await _db.CampaignSettings
            .SingleOrDefaultAsync(setting =>
                setting.Id == id &&
                setting.OwnerUserId ==
                _currentUser.UserId);

    if (setting is null)
    {
      return NotFound();
    }

    _db.CampaignSettings.Remove(
        setting);

    await _db.SaveChangesAsync();

    return NoContent();
  }

  private static SettingDetailDto ToDetailDto(
      CampaignSetting setting,
      int entryCount)
  {
    return new SettingDetailDto(
        setting.Id,
        setting.Name,
        setting.Description,
        entryCount,
        setting.CreatedAt,
        setting.UpdatedAt,
        "GM",
        true);
  }

  private static string? NormalizeDescription(
      string? description)
  {
    if (string.IsNullOrWhiteSpace(
        description))
    {
      return null;
    }

    return description.Trim();
  }
}