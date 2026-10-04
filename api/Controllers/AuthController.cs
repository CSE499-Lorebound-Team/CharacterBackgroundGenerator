using Lorebound.Api.Dtos.Auth;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
  private const string CouldNotRegisterDetail = "Could not register with these details.";

  private readonly UserManager<ApplicationUser> _userManager;

  public AuthController(UserManager<ApplicationUser> userManager)
  {
    _userManager = userManager;
  }

  /// <summary>
  /// Creates an account. Does not sign the user in and returns no token.
  /// </summary>
  [HttpPost("register")]
  [ProducesResponseType<RegisteredUserDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Register(RegisterRequest request)
  {
    var email = request.Email!.Trim();
    var user = new ApplicationUser
    {
      // The email doubles as the Identity username.
      UserName = email,
      Email = email,
      DisplayName = request.DisplayName!.Trim(),
    };

    var result = await _userManager.CreateAsync(user, request.Password!);
    if (!result.Succeeded)
    {
      return RegistrationFailed(result.Errors);
    }

    if (_userManager.Options.SignIn.RequireConfirmedEmail)
    {
      // TODO(P1-05): generate the confirmation token and send the link via
      // IEmailSender<ApplicationUser>.
    }

    return StatusCode(StatusCodes.Status201Created, user.ToRegisteredUserDto());
  }

  private IActionResult RegistrationFailed(IEnumerable<IdentityError> errors)
  {
    var errorList = errors
        // The username is the email, so its errors repeat the email ones and
        // would confuse a client that never sent a username.
        .Where(error => error.Code is not (nameof(IdentityErrorDescriber.DuplicateUserName)
            or nameof(IdentityErrorDescriber.InvalidUserName)))
        .ToList();

    var duplicateEmail = errorList.Any(error =>
        error.Code == nameof(IdentityErrorDescriber.DuplicateEmail));

    if (duplicateEmail && _userManager.Options.SignIn.RequireConfirmedEmail)
    {
      // When accounts need confirming, do not confirm that an email is taken.
      ModelState.AddModelError(string.Empty, CouldNotRegisterDetail);
      return ValidationProblem(ModelState);
    }

    foreach (var error in errorList)
    {
      ModelState.AddModelError(ErrorField(error.Code), error.Description);
    }

    return ValidationProblem(ModelState);
  }

  private static string ErrorField(string code) => code switch
  {
    _ when code.StartsWith("Password", StringComparison.Ordinal) => nameof(RegisterRequest.Password),
    _ when code.Contains("Email", StringComparison.Ordinal) => nameof(RegisterRequest.Email),
    _ => string.Empty,
  };
}
