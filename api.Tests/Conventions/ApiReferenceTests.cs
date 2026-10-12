using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lorebound.Api.Tests.TestSupport;

namespace Lorebound.Api.Tests.Conventions;

// P8-06: docs/api/ holds the OpenAPI document and an endpoint reference
// generated from it. This fails when either is stale, so the committed
// reference always matches the code. To refresh both after an API change:
//   $env:UPDATE_API_DOCS = "1"; dotnet test api.Tests --filter ApiReference; Remove-Item Env:UPDATE_API_DOCS
public partial class ApiReferenceTests : IClassFixture<ApiFactory>
{
  private const string UpdateVariable = "UPDATE_API_DOCS";

  private readonly ApiFactory _factory;

  public ApiReferenceTests(ApiFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Docs_api_matches_the_live_OpenAPI_document()
  {
    var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    var json = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    var markdown = Render(document.RootElement);

    var folder = Path.Combine(RepositoryRoot(), "docs", "api");
    var jsonPath = Path.Combine(folder, "openapi.json");
    var markdownPath = Path.Combine(folder, "README.md");

    if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
    {
      Directory.CreateDirectory(folder);
      await File.WriteAllTextAsync(jsonPath, json);
      await File.WriteAllTextAsync(markdownPath, markdown);
      return;
    }

    var hint = $"docs/api is out of date. Set {UpdateVariable}=1 and rerun this test to regenerate it.";
    Assert.True(File.Exists(jsonPath) && Normalize(await File.ReadAllTextAsync(jsonPath)) == Normalize(json), hint);
    Assert.True(File.Exists(markdownPath) && Normalize(await File.ReadAllTextAsync(markdownPath)) == Normalize(markdown), hint);
  }

  // Git may check files out with CRLF on Windows.
  private static string Normalize(string text) => text.Replace("\r\n", "\n");

  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Lorebound.slnx")))
    {
      directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Lorebound.slnx not found above the test output.");
  }

  private static string Render(JsonElement document)
  {
    var md = new StringBuilder();
    md.Append("# Lorebound API reference\n\n");
    md.Append("<!-- Generated from openapi.json by api.Tests/Conventions/ApiReferenceTests. Do not edit by hand. -->\n\n");
    md.Append("Every endpoint, generated from the OpenAPI document ([`openapi.json`](openapi.json)). ");
    md.Append("The rules behind it (cookie sign-in, the `X-Requested-With` header, problem+json errors, ");
    md.Append("who may do what) are in the root README under \"How the API works\" and in ");
    md.Append("[api/README.md](../../api/README.md). To browse and try the endpoints, run the API in ");
    md.Append("Development and open `http://localhost:5110/scalar`.\n\n");
    md.Append("**Legend:** 🔒 needs the sign-in cookie; ✳ needs the `X-Requested-With: Lorebound` header. ");
    md.Append("Error responses are `application/problem+json`.\n\n");

    var operations = document.GetProperty("paths").EnumerateObject()
        .Where(path => path.Name.StartsWith("/api/"))
        .SelectMany(path => path.Value.EnumerateObject().Select(op => (Path: path.Name, Method: op.Name.ToUpperInvariant(), Op: op.Value)))
        .GroupBy(op => op.Op.GetProperty("tags")[0].GetString()!)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToList();

    md.Append("## Endpoints\n\n");
    foreach (var group in operations)
    {
      md.Append("- [").Append(Words(group.Key)).Append("](#").Append(Anchor(Words(group.Key))).Append(")\n");
    }

    md.Append('\n');

    foreach (var group in operations)
    {
      md.Append("### ").Append(Words(group.Key)).Append("\n\n");
      md.Append("| Endpoint | | Request | Responses |\n|---|---|---|---|\n");
      foreach (var (path, method, op) in group)
      {
        var flags = (op.TryGetProperty("security", out _) ? "🔒" : "") +
            (method is "POST" or "PUT" or "PATCH" or "DELETE" ? "✳" : "");
        md.Append("| `").Append(method).Append(' ').Append(path).Append("`<br>")
            .Append(Cell(op.TryGetProperty("summary", out var summary) ? summary.GetString()! : ""))
            .Append(" | ").Append(flags)
            .Append(" | ").Append(Request(op))
            .Append(" | ").Append(Responses(op))
            .Append(" |\n");
      }

      md.Append('\n');
    }

    md.Append("## Schemas\n\n");
    foreach (var schema in document.GetProperty("components").GetProperty("schemas").EnumerateObject()
        .OrderBy(s => s.Name, StringComparer.Ordinal))
    {
      md.Append("### ").Append(schema.Name).Append("\n\n");
      if (schema.Value.TryGetProperty("enum", out var values))
      {
        md.Append("One of: ")
            .Append(string.Join(", ", values.EnumerateArray().Select(v => "`" + v + "`")))
            .Append("\n\n");
        continue;
      }

      if (!schema.Value.TryGetProperty("properties", out var properties))
      {
        md.Append("`").Append(TypeOf(schema.Value)).Append("`\n\n");
        continue;
      }

      var required = schema.Value.TryGetProperty("required", out var list)
          ? list.EnumerateArray().Select(r => r.GetString()!).ToHashSet()
          : [];
      md.Append("| Field | Type | Required |\n|---|---|---|\n");
      foreach (var property in properties.EnumerateObject())
      {
        md.Append("| `").Append(property.Name).Append("` | ").Append(TypeOf(property.Value))
            .Append(" | ").Append(required.Contains(property.Name) ? "yes" : "")
            .Append(" |\n");
      }

      md.Append('\n');
    }

    return md.ToString();
  }

  private static string Request(JsonElement op)
  {
    var parts = new List<string>();
    if (op.TryGetProperty("parameters", out var parameters))
    {
      foreach (var parameter in parameters.EnumerateArray())
      {
        var location = parameter.GetProperty("in").GetString();
        if (location == "header")
        {
          continue;
        }

        var required = parameter.TryGetProperty("required", out var r) && r.GetBoolean();
        parts.Add($"{location} `{parameter.GetProperty("name").GetString()}`{(required ? "" : "?")}: {TypeOf(parameter.GetProperty("schema"))}");
      }
    }

    if (op.TryGetProperty("requestBody", out var body))
    {
      parts.Add("body: " + TypeOf(body.GetProperty("content").EnumerateObject().First().Value.GetProperty("schema")));
    }

    return parts.Count == 0 ? "" : string.Join("<br>", parts);
  }

  private static string Responses(JsonElement op) =>
      string.Join("<br>", op.GetProperty("responses").EnumerateObject().Select(response =>
      {
        var schema = response.Value.TryGetProperty("content", out var content)
            ? content.EnumerateObject().First().Value.TryGetProperty("schema", out var s) ? TypeOf(s) : ""
            : "";
        return int.Parse(response.Name) >= 400 || schema.Length == 0
            ? $"**{response.Name}**"
            : $"**{response.Name}** {schema}";
      }));

  private static string TypeOf(JsonElement schema)
  {
    if (schema.TryGetProperty("$ref", out var reference))
    {
      var name = reference.GetString()!.Split('/')[^1];
      return $"[{name}](#{Anchor(name)})";
    }

    var types = schema.TryGetProperty("type", out var type)
        ? type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(t => t.GetString()!).ToList()
            : [type.GetString()!]
        : [];
    var nullable = types.Remove("null");
    // Numbers are also accepted as strings, which the document lists as a
    // second type; the API sends them as numbers.
    if (types.Contains("integer") || types.Contains("number"))
    {
      types.Remove("string");
    }

    string text;
    if (schema.TryGetProperty("enum", out var values))
    {
      text = string.Join(" \\| ", values.EnumerateArray().Select(v => "`" + v + "`"));
    }
    else if (types.Contains("array"))
    {
      text = TypeOf(schema.GetProperty("items")) + "[]";
    }
    else if (types.Contains("object") && schema.TryGetProperty("additionalProperties", out var values2)
        && values2.ValueKind == JsonValueKind.Object)
    {
      text = "map of " + TypeOf(values2);
    }
    else
    {
      text = string.Join(" or ", types.Select(t => t + (schema.TryGetProperty("format", out var f) && t != "object" ? $" ({f.GetString()})" : "")));
      if (text.Length == 0)
      {
        text = "any";
      }
    }

    return nullable ? text + " or null" : text;
  }

  private static string Cell(string text) => Whitespace().Replace(text, " ").Trim().Replace("|", "\\|");

  // "SettingEntries" -> "Setting entries".
  private static string Words(string name)
  {
    var words = UpperCase().Replace(name, " $1").Trim().ToLowerInvariant();
    return char.ToUpperInvariant(words[0]) + words[1..];
  }

  private static string Anchor(string heading) => heading.ToLowerInvariant().Replace(' ', '-');

  [GeneratedRegex(@"\s+")]
  private static partial Regex Whitespace();

  [GeneratedRegex("([A-Z])")]
  private static partial Regex UpperCase();
}
