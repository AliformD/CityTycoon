using System.Reflection;

namespace CityTycoon.Engine.Tests.Architecture;

/// <summary>
/// Checks the dependency rule from Section 19.2: the Engine must never depend
/// on the AI, the web UI, the Sim, or the server. If someone accidentally adds
/// such a reference, this test fails and CI turns red.
/// </summary>
public class DependencyRuleTests
{
    [Fact]
    public void Engine_DoesNotReferenceAnyOtherCityTycoonProject()
    {
        Assembly engine = typeof(CityTycoon.Engine.Content.ContentLoader).Assembly;

        var forbidden = engine.GetReferencedAssemblies()
            .Where(a => a.Name!.StartsWith("CityTycoon.", StringComparison.Ordinal))
            .Select(a => a.Name)
            .ToList();

        Assert.Empty(forbidden);
    }
}
