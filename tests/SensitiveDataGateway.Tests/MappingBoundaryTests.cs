using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Tests;

/// <summary>
/// Asserts that the way back from a token to a value stays on this side of the boundary.
/// </summary>
/// <remarks>
/// These are the checks that a plausible, well-meant refactoring would break: moving the map
/// into the shared package so that "everything is in one place", or adding a convenience
/// property that hands the map to whoever already has the document.
/// </remarks>
public sealed class MappingBoundaryTests
{
    private static readonly Type[] s_mappingTypes =
    [
        typeof(PseudonymMap),
        typeof(PseudonymScopeKey),
        typeof(IPseudonymStore),
        typeof(InMemoryPseudonymStore),
        typeof(LocalReidentifier),
        typeof(Pseudonymizer),
        typeof(HashingKey),
        typeof(Hasher),
    ];

    [Fact]
    public void TheMappingTypesAreNotInTheAssemblyAProviderReferences()
    {
        var core = typeof(SanitizedDocument).Assembly;

        Assert.All(s_mappingTypes, type => Assert.NotEqual(core, type.Assembly));
    }

    [Fact]
    public void ASanitizedDocumentHasNoPathToTheMapping() => AssertUnreachable(typeof(SanitizedDocument));

    [Fact]
    public void AValidatedPayloadHasNoPathToTheMapping() => AssertUnreachable(typeof(ValidatedPayload));

    [Fact]
    public void TheAnonymizerContractNamesNothingThatReversesAToken()
    {
        var mentioned = typeof(IDocumentAnonymizer)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(SignatureTypes)
            .Where(s_mappingTypes.Contains)
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(mentioned);
    }

    [Fact]
    public void TheHashingKeyHandsOutNoKeyMaterial()
    {
        var accessors = typeof(HashingKey)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(SignatureTypes)
            .Where(type => type == typeof(byte[]) || type == typeof(ReadOnlyMemory<byte>))
            .ToArray();

        Assert.Empty(accessors);
    }

    private static void AssertUnreachable(Type origin)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            Assert.DoesNotContain(current, s_mappingTypes);

            foreach (var reachable in Reachable(current))
            {
                if (IsOurs(reachable) && seen.Add(reachable))
                {
                    queue.Enqueue(reachable);
                }
            }
        }
    }

    private static IEnumerable<Type> Reachable(Type type) =>
        type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(SignatureTypes)
            .SelectMany(Unwrap);

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                yield return argument;
            }
        }

        if (type.IsArray && type.GetElementType() is { } element)
        {
            yield return element;
        }
    }

    private static bool IsOurs(Type type) =>
        type.Namespace?.StartsWith("SensitiveDataGateway", StringComparison.Ordinal) == true;

    private static IEnumerable<Type> SignatureTypes(MemberInfo member) => member switch
    {
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        MethodInfo method => method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType),
        ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
        _ => [],
    };
}
