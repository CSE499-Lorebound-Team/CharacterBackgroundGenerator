using Lorebound.Api.Sharing;

namespace Lorebound.Api.Tests.Sharing;

// P3-04: codes are read the way a person might type them.
public class InviteCodeNormalizeTests
{
  [Theory]
  [InlineData("7K3M9QXR2B", "7K3M9QXR2B")]
  [InlineData("7k3m9qxr2b", "7K3M9QXR2B")]
  [InlineData("7K3M9-QXR2B", "7K3M9QXR2B")]
  [InlineData(" 7K3M9 QXR2B ", "7K3M9QXR2B")]
  [InlineData("IL0O1ABCDE", "1100" + "1ABCDE")]
  [InlineData("il0o1abcde", "1100" + "1ABCDE")]
  public void Accepts_typed_variants(string input, string expected)
  {
    Assert.True(InviteCodes.TryNormalize(input, out var code));
    Assert.Equal(expected, code);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("7K3M9QXR2")]
  [InlineData("7K3M9QXR2B7")]
  [InlineData("7K3M9QXR2U")]
  [InlineData("7K3M9QXR2!")]
  [InlineData("7K3M9QXR2É")]
  public void Rejects_what_cannot_be_a_code(string? input)
  {
    Assert.False(InviteCodes.TryNormalize(input, out var code));
    Assert.Null(code);
  }

  [Fact]
  public void Generated_codes_normalize_to_themselves()
  {
    for (var i = 0; i < 100; i++)
    {
      var generated = InviteCodes.Generate();
      Assert.True(InviteCodes.TryNormalize(generated.ToLowerInvariant(), out var code));
      Assert.Equal(generated, code);
    }
  }
}
