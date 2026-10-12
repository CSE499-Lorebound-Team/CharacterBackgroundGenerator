using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Errors;

/// <summary>
/// Turns the API's domain exceptions into problem+json responses. Anything
/// else falls through to the default handler, which returns a generic 500
/// without exception details.
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
  private readonly IProblemDetailsService _problemDetails;

  public ApiExceptionHandler(IProblemDetailsService problemDetails)
  {
    _problemDetails = problemDetails;
  }

  public async ValueTask<bool> TryHandleAsync(
      HttpContext httpContext,
      Exception exception,
      CancellationToken cancellationToken)
  {
    var (status, title) = exception switch
    {
      NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
      ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
      ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
      _ => (0, null),
    };

    if (title is null)
    {
      return false;
    }

    httpContext.Response.StatusCode = status;

    var problem = new ProblemDetails
    {
      Status = status,
      Title = title,
      // These messages are written for API clients, so they are safe to return.
      Detail = exception.Message,
    };

    if (exception is ConflictException conflict)
    {
      foreach (var (key, value) in conflict.Extensions)
      {
        problem.Extensions[key] = value;
      }
    }

    return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
    {
      HttpContext = httpContext,
      Exception = exception,
      ProblemDetails = problem,
    });
  }
}
