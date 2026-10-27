using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that the only way to come by a <see cref="ValidatedPayload"/> is to run the
/// gateway.
/// </summary>
/// <remarks>
/// <para>
/// G2 says a payload exists only where the validator made one. The private constructor is
/// half of that property; the other half is that no public member hands one out. A validator
/// whose <c>Validate</c> is public defeats the guarantee without touching the constructor,
/// because every one of its arguments is publicly constructible: a caller can assemble a
/// sanitized document holding whatever text it likes, pass an empty plan so that no check has
/// anything to compare against, and receive a genuine payload with a report that truthfully
/// records ten passed checks.
/// </para>
/// <para>
/// So this test does not look at the constructor. It looks at the public surface of every
/// shipping assembly and asserts that the members which yield a payload are exactly the ones
/// listed here, each of which only re-exposes a payload it was given. A new producer fails
/// this test on the day it is written, which is the day it is still cheap to reconsider.
/// </para>
/// </remarks>
public sealed class PayloadProvenanceTests
{
    /// <summary>
    /// The public members that yield a payload, and why each is not a way to make one.
    /// </summary>
    private static readonly string[] s_permittedReaders =
    [
        // Reads the payload the gateway produced, and is null unless every check passed.
        "SensitiveDataGateway.Validation.SanitizationResult.Payload",

        // Test doubles. Both re-expose a payload the gateway handed to the provider.
        "SensitiveDataGateway.Testing.ProviderCall`1.Payload",
        "SensitiveDataGateway.Testing.RecordingDecisionProvider`2.LastPayload",
    ];

    public static TheoryData<string> ShippingAssemblies()
    {
        var data = new TheoryData<string>();

        foreach (var name in ShippingAssemblyNames())
        {
            data.Add(name);
        }

        return data;
    }

    private static IEnumerable<string> ShippingAssemblyNames() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "SensitiveDataGateway*.dll")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => !name.Contains(".Tests.", StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(ShippingAssemblies))]
    public void No_public_member_produces_a_payload_outside_the_pipeline(string assembly)
    {
        var producers = PayloadReturningMembers(Path.Combine(AppContext.BaseDirectory, assembly))
            .Where(member => !s_permittedReaders.Contains(member, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            producers.Count == 0,
            $"{assembly} hands out a validated payload:{Environment.NewLine}"
            + string.Join(Environment.NewLine, producers));
    }

    [Fact]
    public void The_payload_has_no_public_constructor()
    {
        var constructors = typeof(ValidatedPayload)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        Assert.Empty(constructors);
    }

    [Fact]
    public void The_allowed_result_cannot_be_built_from_outside()
    {
        var factories = typeof(SanitizationResult)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(SanitizationResult))
            .Select(method => method.Name)
            .ToList();

        Assert.True(
            factories.Count == 0,
            $"SanitizationResult has a public factory: {string.Join(", ", factories)}");
    }

    [Fact]
    public void Every_permitted_reader_is_still_on_the_public_surface()
    {
        var surface = ShippingAssemblyNames()
            .SelectMany(name => PayloadReturningMembers(Path.Combine(AppContext.BaseDirectory, name)))
            .ToList();

        var stale = s_permittedReaders.Where(reader => !surface.Contains(reader, StringComparer.Ordinal)).ToList();

        Assert.True(
            stale.Count == 0,
            $"The allow-list names members that no longer exist, so it no longer proves anything:"
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, stale)}");
    }

    [Fact]
    public void The_scan_finds_a_producer_it_is_meant_to_find()
    {
        var members = PayloadReturningMembers(typeof(PayloadProvenanceTests).Assembly.Location);

        Assert.Contains($"{typeof(Decoy).FullName}.Make", members);
    }

    /// <summary>
    /// Every public member of every public type that yields a payload.
    /// </summary>
    private static List<string> PayloadReturningMembers(string path)
    {
        var found = new List<string>();

        foreach (var type in Assembly.LoadFrom(path).GetExportedTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (Yields(method.ReturnType))
                {
                    found.Add($"{type.FullName}.{Named(method)}");
                }
            }

            found.AddRange(
                type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(field => Yields(field.FieldType))
                    .Select(field => $"{type.FullName}.{field.Name}"));
        }

        return found;
    }

    /// <summary>
    /// A payload, or anything a payload can be read out of without a cast.
    /// </summary>
    private static bool Yields(Type type) =>
        type == typeof(ValidatedPayload)
        || (type.IsArray && type.GetElementType() == typeof(ValidatedPayload))
        || (type.IsGenericType && type.GetGenericArguments().Any(argument => argument == typeof(ValidatedPayload)));

    /// <summary>
    /// The member a property getter belongs to, so that the list names what a reader would
    /// have to write rather than the compiler-generated method.
    /// </summary>
    private static string Named(MethodInfo method) =>
        method.IsSpecialName && method.Name.StartsWith("get_", StringComparison.Ordinal)
            ? method.Name[4..]
            : method.Name;

    /// <summary>
    /// Exists so that the scan is proved to find a producer when there is one.
    /// </summary>
    public sealed class Decoy
    {
        public static ValidatedPayload? Make() => null;
    }
}
