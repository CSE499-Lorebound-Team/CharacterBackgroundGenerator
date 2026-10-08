using Lorebound.Api.Sharing;

namespace Lorebound.Api.Tests.Sharing;

// P3-01: invite codes are 10 characters of Crockford base32.
public class InviteCodesTests
{
  [Fact]
  public void Alphabet_is_Crockford_base32()
  {
    Assert.Equal(32, InviteCodes.Alphabet.Distinct().Count());
    Assert.DoesNotContain(InviteCodes.Alphabet, c => "ILOU".Contains(c));
  }

  [Fact]
  public void Codes_have_the_right_length_and_characters()
  {
    for (var i = 0; i < 1000; i++)
    {
      var code = InviteCodes.Generate();

      Assert.Equal(InviteCodes.Length, code.Length);
      Assert.All(code, c => Assert.Contains(c, InviteCodes.Alphabet));
    }
  }

  [Fact]
  public void Codes_are_random()
  {
    var codes = Enumerable.Range(0, 1000).Select(_ => InviteCodes.Generate());

    Assert.Equal(1000, codes.Distinct().Count());
  }

  [Fact]
  public void Every_character_gets_used()
  {
    var used = Enumerable.Range(0, 1000)
        .SelectMany(_ => InviteCodes.Generate())
        .ToHashSet();

    Assert.Equal(InviteCodes.Alphabet.ToHashSet(), used);
  }
}
