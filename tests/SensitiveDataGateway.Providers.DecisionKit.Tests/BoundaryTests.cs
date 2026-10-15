using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace SensitiveDataGateway.Providers.DecisionKit.Tests;

/// <summary>
/// What this package is allowed to know about.
/// </summary>
/// <remarks>
/// <para>
/// The egress guarantee rests on the adapter being unable to map anything but a
/// <see cref="Validation.ValidatedPayload"/>. Reviewing that by reading the source works
/// until someone adds an overload in a hurry, so it is checked mechanically instead: the
/// assembly's type references are read straight out of its metadata, and a reference to a
/// pre-validation type fails the build's test run.
/// </para>
/// <para>
/// This is stricter than it looks. Those types live in the same package as the payload, so
/// the compiler would happily let the adapter name one; nothing but this test stands
/// between a careless change and a document reaching a decision engine unsanitized.
/// </para>
/// </remarks>
public sealed class BoundaryTests
{
    /// <summary>
    /// The types that belong to a stage before validation.
    /// </summary>
    private static readonly string[] s_forbidden =
    [
        "DocumentBundle",
        "DocumentPart",
        "DocumentContent",
        "ExtractedDocument",
        "TextSegment",
        "DetectedDocument",
        "SensitiveEntity",
        "EntityCandidate",
        "PseudonymMap",
        "IPseudonymStore",
        "SanitizedDocument",
    ];

    [Fact]
    public void Names_no_type_from_a_stage_before_validation()
    {
        var referenced = TypeReferences();

        Assert.DoesNotContain(referenced, s_forbidden.Contains);
    }

    /// <summary>
    /// The adapter must still be able to name what it does map.
    /// </summary>
    /// <remarks>
    /// Without this, the test above would also pass on an assembly that was never read at
    /// all — an empty set contains nothing forbidden.
    /// </remarks>
    [Fact]
    public void Names_the_validated_payload()
    {
        var referenced = TypeReferences();

        Assert.Contains("ValidatedPayload", referenced);
    }

    private static HashSet<string> TypeReferences()
    {
        using var stream = System.IO.File.OpenRead(typeof(DecisionKitProvider).Assembly.Location);
        using var reader = new PEReader(stream);

        var metadata = reader.GetMetadataReader();

        return [.. metadata.TypeReferences.Select(handle =>
            metadata.GetString(metadata.GetTypeReference(handle).Name))];
    }
}
