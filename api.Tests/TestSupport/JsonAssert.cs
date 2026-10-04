using System.Text.Json;

namespace Lorebound.Api.Tests.TestSupport;

public static class JsonAssert
{
  /// <summary>
  /// Parses the body and asserts it has exactly these top-level properties,
  /// so a stray token or secret field fails the test.
  /// </summary>
  public static async Task<JsonElement> HasExactlyPropertiesAsync(
      HttpResponseMessage response, params string[] expected)
  {
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = json.RootElement.Clone();

    var actual = root.EnumerateObject().Select(property => property.Name).Order();
    Assert.Equal(expected.Order(), actual);

    return root;
  }

  public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
  {
    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return json.RootElement.Clone();
  }

  public static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
  {
    Assert.Equal(
        "application/problem+json",
        response.Content.Headers.ContentType?.MediaType);

    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    return json.RootElement.Clone();
  }
}
