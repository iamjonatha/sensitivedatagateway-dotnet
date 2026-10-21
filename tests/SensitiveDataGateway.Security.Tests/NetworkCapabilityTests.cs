using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves, over the shipped IL, that nothing which reads raw document content can reach the
/// network.
/// </summary>
/// <remarks>
/// <para>
/// This is the enforcement of the hard rule that <c>Core</c>, the pipeline and the document
/// adapters do no network I/O, and of criterion 1 of
/// <c>docs/adr/0018-format-adapters-may-take-a-parser.md</c>. Both are properties of what an
/// assembly is built from rather than of what a test can observe it doing: an adapter that
/// never opens a socket, but ships a parser that could, still puts the capability on the
/// machine and still makes the dependency graph answer "yes" to the only question that is
/// cheap to ask of it.
/// </para>
/// <para>
/// Reviewing this by reading release notes does not scale and does not survive an upgrade.
/// Reading the IL does: a dependency that acquires the capability in a later version fails
/// this test on the day it is taken, which is the day it can still be declined.
/// </para>
/// </remarks>
public sealed class NetworkCapabilityTests
{
    /// <summary>
    /// The namespaces whose every type can move bytes off the process.
    /// </summary>
    private static readonly string[] s_networkNamespaces =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.Security",
        "System.Net.WebSockets",
        "System.Net.Quic",
    ];

    /// <summary>
    /// The individual types that perform network I/O from namespaces that mostly do not.
    /// </summary>
    /// <remarks>
    /// <c>System.Net</c> and <c>System.Net.Mail</c> are not banned wholesale. Both are mostly
    /// in-memory: <c>MailMessage</c>, <c>MailAddress</c> and <c>Attachment</c> are the objects
    /// a parser converts to and from, <c>System.Net.Mime.ContentType</c> parses a header, and
    /// <c>IPAddress.TryParse</c> validates an address literal in a <c>Received</c> line. None
    /// of them opens anything. Banning the namespaces would make this test fire on all of that
    /// and the only way to keep the build green would be to stop running it. Naming the types
    /// that actually transmit keeps the check precise enough to be believed.
    /// </remarks>
    private static readonly string[] s_networkTypes =
    [
        "System.Net.Dns",
        "System.Net.WebClient",
        "System.Net.WebRequest",
        "System.Net.HttpWebRequest",
        "System.Net.FtpWebRequest",
        "System.Net.Mail.SmtpClient",
        "System.Net.NetworkInformation.Ping",
    ];

    /// <summary>
    /// The assemblies that read raw content, and everything they carry.
    /// </summary>
    /// <remarks>
    /// The parsers are named explicitly rather than discovered, so that a dependency added
    /// without a thought about this file is not silently covered — or silently missed.
    /// </remarks>
    public static TheoryData<string> ContentReadingAssemblies =>
    [
        "SensitiveDataGateway.Core.dll",
        "SensitiveDataGateway.dll",
        "SensitiveDataGateway.Documents.Mime.dll",
        "MimeKitLite.dll",
        "SensitiveDataGateway.Documents.OpenXml.dll",
        "DocumentFormat.OpenXml.dll",
        "DocumentFormat.OpenXml.Framework.dll",
        "System.IO.Packaging.dll",
        "SensitiveDataGateway.Documents.Pdf.dll",
        "UglyToad.PdfPig.dll",
        "UglyToad.PdfPig.Core.dll",
        "UglyToad.PdfPig.DocumentLayoutAnalysis.dll",
        "UglyToad.PdfPig.Fonts.dll",
        "UglyToad.PdfPig.Package.dll",
        "UglyToad.PdfPig.Tokenization.dll",
        "UglyToad.PdfPig.Tokens.dll",
    ];

    [Theory]
    [MemberData(nameof(ContentReadingAssemblies))]
    public void Nothing_that_reads_content_can_reach_the_network(string assembly)
    {
        var path = Path.Combine(AppContext.BaseDirectory, assembly);

        Assert.True(File.Exists(path), $"{assembly} is not in the output; the test cannot vouch for it.");

        var calls = AssemblyCapabilityScanner.FindCallsInto(path, s_networkNamespaces, s_networkTypes);

        Assert.True(
            calls.Count == 0,
            $"{assembly} calls into the network:{Environment.NewLine}{string.Join(Environment.NewLine, calls)}");
    }

    [Fact]
    public void The_scanner_finds_a_call_it_is_meant_to_find()
    {
        var self = typeof(NetworkCapabilityTests).Assembly.Location;

        var calls = AssemblyCapabilityScanner.FindCallsInto(self, ["System.Reflection.PortableExecutable"], []);

        Assert.NotEmpty(calls);
    }

    [Fact]
    public void The_scanner_finds_a_type_it_is_meant_to_find()
    {
        var self = typeof(NetworkCapabilityTests).Assembly.Location;

        var calls = AssemblyCapabilityScanner.FindCallsInto(self, [], ["System.IO.File"]);

        Assert.NotEmpty(calls);
    }

    [Fact]
    public void The_scanner_reports_nothing_for_a_namespace_that_is_absent()
    {
        var self = typeof(NetworkCapabilityTests).Assembly.Location;

        var calls = AssemblyCapabilityScanner.FindCallsInto(self, ["Nowhere.At.All"], ["Nowhere.At.All.Thing"]);

        Assert.Empty(calls);
    }

    [Fact]
    public void The_scanner_does_not_match_a_namespace_that_merely_starts_the_same_way()
    {
        var self = typeof(NetworkCapabilityTests).Assembly.Location;

        var calls = AssemblyCapabilityScanner.FindCallsInto(self, ["System.I"], []);

        Assert.Empty(calls);
    }
}
