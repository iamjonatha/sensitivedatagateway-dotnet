using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Reads the shipped IL of an assembly and reports which methods call into a namespace.
/// </summary>
/// <remarks>
/// This is a check on the build output rather than on the sources, which is the point: a
/// capability arrives through a dependency, and a dependency is not visible in this
/// repository's code. Type references alone are not enough to answer the question, because a
/// parser legitimately mentions in-memory types such as <c>System.Net.Mime.ContentType</c>.
/// What matters is whether a method body calls one, so the scan walks method bodies and
/// matches the metadata token of every call, callvirt, newobj and field access against the
/// member references that resolve to the namespaces in question.
/// </remarks>
internal static class AssemblyCapabilityScanner
{
    /// <summary>
    /// Returns every method that calls into one of the given namespaces or types.
    /// </summary>
    /// <param name="assemblyPath">The assembly to read.</param>
    /// <param name="namespaces">Namespace prefixes, every type in them matching.</param>
    /// <param name="types">Full names of individual types that match.</param>
    /// <returns>One entry per call site, as <c>Type::Method -> Target::Member</c>.</returns>
    public static IReadOnlyList<string> FindCallsInto(
        string assemblyPath,
        IReadOnlyList<string> namespaces,
        IReadOnlyList<string> types)
    {
        using var file = File.OpenRead(assemblyPath);
        using var image = new PEReader(file);
        var metadata = image.GetMetadataReader();
        var members = MembersIn(metadata, namespaces, types);

        if (members.Count == 0)
        {
            return [];
        }

        var found = new List<string>();

        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            var typeName = $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";

            foreach (var methodHandle in type.GetMethods())
            {
                AddCallSites(metadata, image, members, typeName, methodHandle, found);
            }
        }

        return found;
    }

    private static void AddCallSites(
        MetadataReader metadata,
        PEReader image,
        Dictionary<int, string> members,
        string typeName,
        MethodDefinitionHandle methodHandle,
        List<string> found)
    {
        var method = metadata.GetMethodDefinition(methodHandle);

        if (method.RelativeVirtualAddress == 0)
        {
            return;
        }

        var il = image.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();

        if (il is null)
        {
            return;
        }

        var name = $"{typeName}::{metadata.GetString(method.Name)}";

        foreach (var target in TargetsIn(il, members))
        {
            found.Add($"{name} -> {target}");
        }
    }

    /// <summary>
    /// Returns the member references of an assembly that resolve to the given namespaces or types.
    /// </summary>
    /// <param name="metadata">The metadata of the assembly.</param>
    /// <param name="namespaces">Namespace prefixes, every type in them matching.</param>
    /// <param name="types">Full names of individual types that match.</param>
    /// <returns>The members, by metadata token.</returns>
    private static Dictionary<int, string> MembersIn(
        MetadataReader metadata,
        IReadOnlyList<string> namespaces,
        IReadOnlyList<string> types)
    {
        var matched = new Dictionary<int, string>();

        foreach (var handle in metadata.TypeReferences)
        {
            var type = metadata.GetTypeReference(handle);
            var @namespace = metadata.GetString(type.Namespace);
            var fullName = $"{@namespace}.{metadata.GetString(type.Name)}";

            if (Matches(@namespace, namespaces) || types.Contains(fullName, StringComparer.Ordinal))
            {
                matched[MetadataTokens.GetToken(handle)] = fullName;
            }
        }

        var members = new Dictionary<int, string>();

        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);

            if (matched.TryGetValue(MetadataTokens.GetToken(member.Parent), out var owner))
            {
                members[MetadataTokens.GetToken(handle)] = $"{owner}::{metadata.GetString(member.Name)}";
            }
        }

        return members;
    }

    /// <summary>
    /// Returns the matching members a method body refers to.
    /// </summary>
    /// <param name="il">The method body.</param>
    /// <param name="members">The members to look for, by metadata token.</param>
    /// <returns>The members found.</returns>
    /// <remarks>
    /// Opcodes are matched by their single byte, and the four bytes after one are read as a
    /// token. An operand of an unrelated instruction can coincide with one of these bytes, so
    /// a match is only ever a token that is also in <paramref name="members"/>; a false
    /// positive would have to collide with a real member token, and a false positive here
    /// fails a build rather than passing one.
    /// </remarks>
    private static SortedSet<string> TargetsIn(byte[] il, Dictionary<int, string> members)
    {
        var targets = new SortedSet<string>(StringComparer.Ordinal);

        for (var index = 0; index + 4 < il.Length; index++)
        {
            if (!IsTokenOpcode(il[index]))
            {
                continue;
            }

            if (members.TryGetValue(BitConverter.ToInt32(il, index + 1), out var member))
            {
                targets.Add(member);
            }
        }

        return targets;
    }

    private static bool IsTokenOpcode(byte opcode) =>
        opcode is 0x28 or 0x6F or 0x73 or 0x7B or 0x7C or 0x7D or 0x7E or 0x7F;

    private static bool Matches(string @namespace, IReadOnlyList<string> prefixes) =>
        prefixes.Any(prefix =>
            string.Equals(@namespace, prefix, StringComparison.Ordinal)
            || @namespace.StartsWith($"{prefix}.", StringComparison.Ordinal));
}
