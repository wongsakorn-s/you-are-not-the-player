using Game.Sim.Time;

namespace Game.Sim.Tests.Architecture;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void SimulationAssembly_DoesNotReferenceGodot()
    {
        string[] references = typeof(SimClock)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            reference.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
    }
}
