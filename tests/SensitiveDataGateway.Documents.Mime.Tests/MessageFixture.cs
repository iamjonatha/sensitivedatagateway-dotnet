using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Documents.Mime;

namespace SensitiveDataGateway.Documents.Mime.Tests;

/// <summary>
/// Builds the messages the reader tests read.
/// </summary>
/// <remarks>
/// Every address, name and file name in here is invented. None of it belongs to anybody.
/// </remarks>
internal static class MessageFixture
{
    public static MemoryStream Message(params string[] lines) =>
        new(Encoding.ASCII.GetBytes(string.Join("\r\n", lines)));

    public static MemoryStream Raw(byte[] bytes) => new(bytes);

    public static ProcessingLimits Limits(
        int maxPartCount = 64,
        long maxPartBytes = 65_536,
        long maxBundleBytes = 262_144) =>
        ProcessingLimits.Create(
            maxBundleBytes,
            maxPartBytes,
            maxPartCount,
            maxExtractedCharacters: 1_000_000,
            maxEntities: 1_000,
            maxProcessingTime: TimeSpan.FromMinutes(1));

    public static Task<DocumentBundle> ReadAsync(Stream message, MimeReaderOptions? options = null) =>
        new MimeBundleReader(options ?? MimeReaderOptions.Default)
            .ReadAsync(BundleId.Create("bundle"), message, Limits(), CancellationToken.None);

    public static Task<DocumentBundle> ReadAsync(Stream message, ProcessingLimits limits) =>
        new MimeBundleReader().ReadAsync(BundleId.Create("bundle"), message, limits, CancellationToken.None);

    public static Task<ExtractedDocument> ExtractAsync(DocumentBundle bundle) =>
        new ExtractionPipeline([new MimeStructureExtractor(), new PlainTextExtractor()])
            .ExtractAsync(bundle, ProcessingLimits.Default, CancellationToken.None);

    public static string TextOf(DocumentPart part) => part.Content.AsText();

    public static IEnumerable<string> MediaTypes(DocumentBundle bundle) =>
        bundle.Parts.Select(part => part.MediaType);

    public static DocumentPart PartWithMediaType(DocumentBundle bundle, string mediaType) =>
        bundle.Parts.Single(part => part.MediaType == mediaType);
}
