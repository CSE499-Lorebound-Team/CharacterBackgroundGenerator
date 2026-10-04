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

  public AuthController(
      UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
      TimeProvider timeProvider,
      ILogger<AuthController> logger)
  {
    _userManager = userManager;
    _signInManager = signInManager;
    _timeProvider = timeProvider;
    _logger = logger;
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

  /// <summary>
  /// Signs in and sets the httpOnly auth cookie. The body is only the user
  /// summary; wrong password, unknown email and unconfirmed email all get the
  /// same 401 so the response does not reveal which accounts exist.
  /// </summary>
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
