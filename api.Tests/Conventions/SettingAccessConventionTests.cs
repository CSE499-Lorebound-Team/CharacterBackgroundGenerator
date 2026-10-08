using System.Reflection;
using Lorebound.Api.Auth;
using Lorebound.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Lorebound.Api.Tests.Conventions;

// P2-02: every controller that serves setting data must authorize through
// ISettingAccess, including the ones later phases add under api/settings.
public class SettingAccessConventionTests
{
  private static IEnumerable<Type> SettingControllers() =>
      typeof(SettingsController).Assembly
          .GetTypes()
          .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
          .Where(type => type.GetCustomAttribute<RouteAttribute>()?.Template
              .StartsWith("api/settings", StringComparison.OrdinalIgnoreCase) == true);

  [Fact]
  public void SettingsController_is_found()
  {
    Assert.Contains(typeof(SettingsController), SettingControllers());
  }

  [Fact]
  public void Every_settings_controller_takes_ISettingAccess()
  {
    var missing = SettingControllers()
        .Where(type => !type.GetConstructors()
            .Any(ctor => ctor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(ISettingAccess))))
        .Select(type => type.Name)
        .ToList();

    Assert.True(missing.Count == 0,
        "These controllers serve setting data without ISettingAccess: " + string.Join(", ", missing));
  }
}
