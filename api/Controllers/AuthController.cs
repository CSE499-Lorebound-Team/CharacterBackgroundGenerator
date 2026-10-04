using Lorebound.Api.Auth;
using Lorebound.Api.Dtos.Auth;
using Lorebound.Api.Mapping;
using Lorebound.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
  private const string InvalidCredentialsDetail = "Invalid email or password.";
  private const string CouldNotRegisterDetail = "Could not register with these details.";

  // A real hash to verify against when the email is unknown, so a miss costs
  // the same hashing time as a wrong password. Uses the default hasher
  // settings, which are the ones Identity is registered with.
  private static readonly Lazy<string> DummyPasswordHash = new(() =>
      new PasswordHasher<ApplicationUser>().HashPassword(
          new ApplicationUser(), Guid.NewGuid().ToString()));

  private readonly UserManager<ApplicationUser> _userManager;
  private readonly SignInManager<ApplicationUser> _signInManager;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<AuthController> _logger;
  private readonly IEmailSender<ApplicationUser> _emailSender;
  private readonly FrontendLinks _links;

  public AuthController(
      UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
      TimeProvider timeProvider,
      ILogger<AuthController> logger,
      IEmailSender<ApplicationUser> emailSender,
      FrontendLinks links)
  {
    _userManager = userManager;
    _signInManager = signInManager;
    _timeProvider = timeProvider;
    _logger = logger;
    _emailSender = emailSender;
    _links = links;
  }

  /// <summary>
  /// Creates an account. Does not sign the user in and returns no token.
  /// </summary>
  [AllowAnonymous]
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

    // Sent even when sign-in does not require confirmation (Development), so
    // the confirm flow can be exercised locally; Auth:RequireConfirmedEmail
    // only gates login.
    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    await _emailSender.SendConfirmationLinkAsync(
        user, email, _links.ConfirmEmail(user.Id, token));

    return StatusCode(StatusCodes.Status201Created, user.ToRegisteredUserDto());
  }

  /// <summary>
  /// Signs in and sets the httpOnly auth cookie. The body is only the user
  /// summary; wrong password, unknown email and unconfirmed email all get the
  /// same 401 so the response does not reveal which accounts exist.
  /// </summary>
  [AllowAnonymous]
  [HttpPost("login")]
  [ProducesResponseType<AuthUserDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status423Locked)]
  public async Task<IActionResult> Login(LoginRequest request)
  {
    var user = await _userManager.FindByEmailAsync(request.Email!.Trim());
    if (user is null)
    {
      BurnPasswordCheck(request.Password!);
      return InvalidCredentials();
    }

    var result = await _signInManager.PasswordSignInAsync(
        user, request.Password!, isPersistent: request.RememberMe, lockoutOnFailure: true);

    if (result.Succeeded)
    {
      return Ok(user.ToAuthUserDto());
    }

    if (result.IsLockedOut)
    {
      _logger.LogWarning("Sign-in refused for locked-out user {UserId}.", user.Id);
      return LockedOut(user);
    }

    if (result.IsNotAllowed)
    {
      // Identity rejects an unconfirmed email before checking the password.
      BurnPasswordCheck(request.Password!);
    }

    return InvalidCredentials();
  }

  /// <summary>
  /// Signs out by expiring the auth cookie (same name and path as at sign-in).
  /// </summary>
  [Authorize]
  [HttpPost("logout")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Logout()
  {
    await _signInManager.SignOutAsync();
    return NoContent();
  }

  /// <summary>
  /// Confirms an email with the userId and code from the emailed link. An
  /// unknown user, a malformed or wrong code, and an already-confirmed account
  /// all get the same 400, so a code cannot be reused.
  /// </summary>
  [AllowAnonymous]
  [HttpPost("confirm-email")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
  {
    var user = await _userManager.FindByIdAsync(request.UserId!.Value.ToString());

    // Identity would accept the same code twice, so a confirmed account is
    // treated as a used link.
    if (user is null
        || user.EmailConfirmed
        || !EmailCodes.TryDecode(request.Code!, out var token)
        || !(await _userManager.ConfirmEmailAsync(user, token)).Succeeded)
    {
      return InvalidLink("This confirmation link is invalid or has already been used.");
    }

    return NoContent();
  }

  /// <summary>
  /// Sends a fresh confirmation link. Always 204, whether the email is
  /// unknown, already confirmed or sent to, so it reveals nothing.
  /// </summary>
  [AllowAnonymous]
  [HttpPost("resend-confirmation")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
  {
    var user = await _userManager.FindByEmailAsync(request.Email!.Trim());
    if (user is { EmailConfirmed: false })
    {
      var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
      await _emailSender.SendConfirmationLinkAsync(
          user, user.Email!, _links.ConfirmEmail(user.Id, token));
    }

    return NoContent();
  }

  /// <summary>
  /// Emails a password reset link. Always 204; the link goes only to an
  /// existing account whose email is confirmed, since that proves the inbox
  /// belongs to the account.
  /// </summary>
  [AllowAnonymous]
  [HttpPost("forgot-password")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
  {
    var user = await _userManager.FindByEmailAsync(request.Email!.Trim());
    if (user is { EmailConfirmed: true })
    {
      var token = await _userManager.GeneratePasswordResetTokenAsync(user);
      await _emailSender.SendPasswordResetLinkAsync(
          user, user.Email!, _links.ResetPassword(user.Email!, token));
    }

    return NoContent();
  }

  /// <summary>
  /// Sets a new password with the email and code from the reset link. Identity
  /// changes the security stamp, which ends the user's other sessions and
  /// makes the code single-use.
  /// </summary>
  [AllowAnonymous]
  [HttpPost("reset-password")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
  {
    const string invalidResetLink = "This reset link is invalid or has expired.";

    var user = await _userManager.FindByEmailAsync(request.Email!.Trim());
    if (user is null || !EmailCodes.TryDecode(request.Code!, out var token))
    {
      return InvalidLink(invalidResetLink);
    }

    var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword!);
    if (result.Succeeded)
    {
      return NoContent();
    }

    if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken)))
    {
      return InvalidLink(invalidResetLink);
    }

    // Only password policy errors remain.
    foreach (var error in result.Errors)
    {
      ModelState.AddModelError(nameof(ResetPasswordRequest.NewPassword), error.Description);
    }

    return ValidationProblem(ModelState);
  }

  private IActionResult InvalidLink(string detail) =>
      Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Bad Request",
          detail: detail);

  private void BurnPasswordCheck(string password) =>
      _userManager.PasswordHasher.VerifyHashedPassword(
          new ApplicationUser(), DummyPasswordHash.Value, password);

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

  private IActionResult InvalidCredentials() =>
      Problem(
          statusCode: StatusCodes.Status401Unauthorized,
          title: "Unauthorized",
          detail: InvalidCredentialsDetail);

  private IActionResult LockedOut(ApplicationUser user)
  {
    var remaining = (user.LockoutEnd ?? _timeProvider.GetUtcNow()) - _timeProvider.GetUtcNow();
    var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));

    Response.Headers.RetryAfter = retryAfterSeconds.ToString();

    var problem = ProblemDetailsFactory.CreateProblemDetails(
        HttpContext,
        statusCode: StatusCodes.Status423Locked,
        title: "Account locked",
        detail: "Too many failed sign-in attempts. Try again later.");
    problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

    return new ObjectResult(problem)
    {
      StatusCode = StatusCodes.Status423Locked,
      ContentTypes = { "application/problem+json" },
    };
  }
}
