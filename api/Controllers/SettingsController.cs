using Lorebound.Api.Auth;
using Lorebound.Api.Data;
using Lorebound.Api.Dtos.Settings;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lorebound.Api.Errors;

namespace Lorebound.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
private readonly LoreboundDbContext _db;
private readonly ICurrentUser _currentUser;
private readonly ISettingAccess _settingAccess;

  public SettingsController(
      LoreboundDbContext db,
      ICurrentUser currentUser,
      ISettingAccess settingAccess)
  {
    _db = db;
    _currentUser = currentUser;
    _settingAccess = settingAccess;
  }

  [HttpPost]
[ProducesResponseType<SettingDetailDto>(
    StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<ActionResult<SettingDetailDto>> Create(
    CreateSettingRequest request)
    {
    var name = request.Name.Trim();

    var duplicateExists =
        await _db.CampaignSettings
            .AsNoTracking()
            .AnyAsync(setting =>
                setting.OwnerUserId == _currentUser.UserId &&
                EF.Functions.ILike(
                    setting.Name,
                    name));

    if (duplicateExists)
    {
        throw new ConflictException(
            "A setting with this name already exists.");
    }

    var setting = new CampaignSetting
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description =
            NormalizeDescription(
                request.Description),
        OwnerUserId =
            _currentUser.UserId,
    };

    var membership = new SettingMembership
    {
        Id = Guid.NewGuid(),
        CampaignSettingId = setting.Id,
        UserId = _currentUser.UserId,
        Role = SettingRole.GameMaster,
        JoinedAt = DateTimeOffset.UtcNow,
    };

    _db.CampaignSettings.Add(setting);
    _db.SettingMemberships.Add(membership);

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
    await _settingAccess
        .VisibleToCurrentUser()
        .AsNoTracking()
        .OrderByDescending(setting =>
            setting.UpdatedAt)
        .Select(setting =>
            new SettingListItemDto(
                setting.Id,
                setting.Name,
                setting.Description,
                setting.Entries.Count,
                setting.UpdatedAt,
                setting.OwnerUserId ==
                    _currentUser.UserId
                    ? "GameMaster"
                    : setting.Memberships
                        .Where(membership =>
                            membership.UserId ==
                            _currentUser.UserId)
                        .Select(membership =>
                            membership.Role.ToString())
                        .First(),
                setting.OwnerUserId ==
                    _currentUser.UserId))
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
      await _settingAccess.RequireMemberAsync(id);

  var role =
      setting.OwnerUserId == _currentUser.UserId
          ? SettingRole.GameMaster
          : await _settingAccess.GetRoleAsync(id);

  var entryCount =
      await _db.SettingEntries
          .CountAsync(entry =>
              entry.CampaignSettingId == id);

  return Ok(
      new SettingDetailDto(
          setting.Id,
          setting.Name,
          setting.Description,
          entryCount,
          setting.CreatedAt,
          setting.UpdatedAt,
          role?.ToString() ?? "GameMaster",
          setting.OwnerUserId ==
              _currentUser.UserId));
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
      await _settingAccess.RequireGameMasterAsync(id);

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
      await _settingAccess.RequireOwnerAsync(id);

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