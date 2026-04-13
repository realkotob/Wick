using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Wick.Tests.Unit;

/// <summary>
/// MSBuildLocator.RegisterDefaults() is process-global and must be called exactly once
/// before any Microsoft.Build type is touched. Per-class static constructors do not
/// coordinate across classes, so multiple test classes that each try to register race
/// on CI. A module initializer runs once, deterministically, before any test class is
/// loaded — which is the only safe place to do this.
/// </summary>
internal static class MSBuildLocatorSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }
}
