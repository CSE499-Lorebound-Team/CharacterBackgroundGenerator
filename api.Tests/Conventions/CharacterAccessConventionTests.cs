using System.Reflection;
using Lorebound.Api.Auth;
using Lorebound.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Tests.Conventions;

// P6-02: every controller that serves characters must authorize through
// ICharacterAccess, including ones later phases add under api/characters.
public class CharacterAccessConventionTests
{
  private static IEnumerable<Type> CharacterControllers() =>
      typeof(CharactersController).Assembly
          .GetTypes()
          .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
          .Where(type => type.GetCustomAttribute<RouteAttribute>()?.Template
              .StartsWith("api/characters", StringComparison.OrdinalIgnoreCase) == true);

  [Fact]
  public void CharactersController_is_found()
  {
    Assert.Contains(typeof(CharactersController), CharacterControllers());
  }

  [Fact]
  public void Every_characters_controller_takes_ICharacterAccess()
  {
    var missing = CharacterControllers()
        .Where(type => !type.GetConstructors()
            .Any(ctor => ctor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(ICharacterAccess))))
        .Select(type => type.Name)
        .ToList();

    Assert.True(missing.Count == 0,
        "These controllers serve characters without ICharacterAccess: " + string.Join(", ", missing));
  }
}
