using System.Reflection;
using Game.Sim.Cases;

namespace Game.Sim.Tests.Cases;

/// <summary>
/// Guards the rule from the design document: hidden truth must not leak into
/// WorldEvent, Observation, Memory or Suspicion. NPCs are only ever allowed to
/// infer who the Player is from behaviour that actually happened, so if any of
/// those pipelines could read a <see cref="SessionTruth"/>, the deduction game
/// would be decided by a flag rather than by evidence.
/// </summary>
public sealed class SessionTruthIsolationTests
{
    private static readonly string[] ForbiddenNamespaces =
    [
        "Game.Sim.Events",
        "Game.Sim.Perception",
        "Game.Sim.Memory",
        "Game.Sim.Suspicion",
        "Game.Sim.Behaviors",
        "Game.Sim.Patterns",
    ];

    private static readonly Type[] TruthTypes =
    [
        typeof(SessionTruth),
        typeof(SecretAssignment),
        typeof(AnomalyBeat),
        typeof(CaseGenerationOptions),
    ];

    [Fact]
    public void PerceptionPipelineTypesNeverTouchHiddenTruth()
    {
        const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var leaks = new List<string>();
        IEnumerable<Type> pipelineTypes = typeof(SessionTruth).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null)
            .Where(type => ForbiddenNamespaces.Contains(type.Namespace, StringComparer.Ordinal));

        foreach (Type type in pipelineTypes)
        {
            foreach (FieldInfo field in type.GetFields(AllMembers))
            {
                if (IsTruth(field.FieldType))
                {
                    leaks.Add($"{type.FullName}.{field.Name} (field)");
                }
            }

            foreach (PropertyInfo property in type.GetProperties(AllMembers))
            {
                if (IsTruth(property.PropertyType))
                {
                    leaks.Add($"{type.FullName}.{property.Name} (property)");
                }
            }

            foreach (MethodBase method in type
                .GetMethods(AllMembers)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(AllMembers)))
            {
                if (method is MethodInfo { ReturnType: { } returnType } && IsTruth(returnType))
                {
                    leaks.Add($"{type.FullName}.{method.Name} (returns)");
                }

                leaks.AddRange(method
                    .GetParameters()
                    .Where(parameter => IsTruth(parameter.ParameterType))
                    .Select(parameter =>
                        $"{type.FullName}.{method.Name}({parameter.Name}) (parameter)"));
            }
        }

        Assert.True(
            leaks.Count == 0,
            "Hidden truth reached the perception pipeline: " + string.Join(", ", leaks));
    }

    [Fact]
    public void HiddenTruthLivesInItsOwnNamespace() =>
        Assert.All(TruthTypes, type => Assert.Equal("Game.Sim.Cases", type.Namespace));

    private static bool IsTruth(Type type)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsByRef || target.IsArray)
        {
            target = target.GetElementType() ?? target;
        }

        return TruthTypes.Contains(target) ||
            (target.IsGenericType && target.GetGenericArguments().Any(IsTruth));
    }
}
