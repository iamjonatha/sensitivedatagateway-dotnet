using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Asserts that the pipeline assembly cannot do the things it promises not to do.
/// </summary>
/// <remarks>
/// <para>
/// The promise that no detector reaches the network is the one the whole design rests on
/// ([ADR 0009](../../../docs/adr/0009-no-network-in-core.md)). A promise kept by everybody
/// remembering it is a promise that lasts until the first contractor adds a lookup against a
/// validation service, so it is checked here instead, by reading what the compiled assembly
/// refers to.
/// </para>
/// <para>
/// This is deliberately coarser than "no detector". If no type in the assembly refers to a
/// socket, an HTTP client, a file or a database, then no detector in it can use one, and no
/// future one can either without this test failing first.
/// </para>
/// <para>
/// In-memory stream types are not on the list. An extractor reads the stream the caller
/// supplies through <see cref="Documents.DocumentContent.OpenStreamAsync"/>, which is content
/// the application already has; what the assembly must never do is go and find content, or
/// send it, of its own accord.
/// </para>
/// </remarks>
public sealed class DetectionBoundaryTests
{
    private static readonly string[] s_forbiddenNamespaces =
    [
        "System.Net",
        "System.Data",
        "System.IO.Pipes",
        "System.IO.MemoryMappedFiles",
        "Microsoft.Extensions.Http",
    ];

    private static readonly string[] s_forbiddenTypes =
    [
        "System.Diagnostics.Process",
        "System.Diagnostics.ProcessStartInfo",
        "System.IO.Directory",
        "System.IO.DirectoryInfo",
        "System.IO.File",
        "System.IO.FileInfo",
        "System.IO.FileStream",
        "System.IO.FileSystemWatcher",
        "System.IO.Path",
    ];

    [Fact]
    public void The_pipeline_assembly_refers_to_nothing_that_reaches_the_network_or_the_file_system()
    {
        var forbidden = TypeReferencesOf(typeof(DetectionPipeline).Assembly.Location)
            .Select(reference => (reference.Namespace, Full: $"{reference.Namespace}.{reference.Name}"))
            .Where(reference => IsForbidden(reference.Namespace, reference.Full))
            .Select(reference => reference.Full)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(forbidden);
    }

    private static bool IsForbidden(string space, string fullName) =>
        s_forbiddenTypes.Contains(fullName, StringComparer.Ordinal)
        || s_forbiddenNamespaces.Any(forbidden =>
            string.Equals(space, forbidden, StringComparison.Ordinal)
            || space.StartsWith($"{forbidden}.", StringComparison.Ordinal));

    [Fact]
    public void Every_built_in_detector_lives_in_that_assembly()
    {
        var assembly = typeof(DetectionPipeline).Assembly;

        Assert.All(
            DeterministicDetectors.All(),
            detector => Assert.Same(assembly, detector.GetType().Assembly));
    }

    private static List<(string Namespace, string Name)> TypeReferencesOf(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var portableExecutable = new PEReader(stream);

        var metadata = portableExecutable.GetMetadataReader();
        var references = new List<(string Namespace, string Name)>();

        foreach (var handle in metadata.TypeReferences)
        {
            var reference = metadata.GetTypeReference(handle);

            references.Add((
                metadata.GetString(reference.Namespace),
                metadata.GetString(reference.Name)));
        }

        return references;
    }
}
