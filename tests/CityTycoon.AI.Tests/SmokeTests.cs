namespace CityTycoon.AI.Tests;

public class SmokeTests
{
    // A placeholder so the AI test project runs in CI from day one.
    // Real bot tests arrive with the first bot in Phase 1.
    [Fact]
    public void AiProject_CanSeeTheEngine()
    {
        var rules = CityTycoon.Engine.Content.ContentLoader.LoadRules();
        Assert.True(rules.StartingCash > 0);
    }
}
