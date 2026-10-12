namespace Lorebound.Api.Errors;

public class ConflictException : Exception
{
  public ConflictException(string message = "The request conflicts with the current state of the resource.")
      : base(message)
  {
  }

  /// <summary>
  /// Adds <paramref name="extensions"/> as extra problem+json members, e.g.
  /// <c>{ relationshipCount }</c>, so the client can explain the conflict.
  /// </summary>
  public ConflictException(string message, IReadOnlyDictionary<string, object?> extensions)
      : base(message)
  {
    Extensions = extensions;
  }

  public IReadOnlyDictionary<string, object?> Extensions { get; }
      = new Dictionary<string, object?>();
}
