using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Documents.Recognition;

namespace SensitiveDataGateway.Tests.Documents;

/// <summary>
/// A recognizer that returns what the test told it to return.
/// </summary>
/// <remarks>
/// Every value in here is invented. None of it is a real name, address or account.
/// </remarks>
internal sealed class FakeRecognizer : ITextRecognizer
{
    private readonly Func<DocumentPart, RecognizedText?> _read;

    private FakeRecognizer(RecognizerVersion version, Func<DocumentPart, RecognizedText?> read)
    {
        Version = version;
        _read = read;
    }

    public RecognizerVersion Version { get; }

    public int Calls { get; private set; }

    public static FakeRecognizer Reading(TrustBoundaryLocality locality, params (string Text, double Confidence)[] lines) =>
        new(
            VersionFor(locality),
            _ => RecognizedText.Create(
                lines.Select((line, index) => RecognizedLine.Create(line.Text, line.Confidence, index + 1))));

    public static FakeRecognizer Reading(params (string Text, double Confidence)[] lines) =>
        Reading(TrustBoundaryLocality.InProcess, lines);

    public static FakeRecognizer ReadingNothing() =>
        new(VersionFor(TrustBoundaryLocality.InProcess), _ => RecognizedText.Empty());

    public static FakeRecognizer ReturningNull() =>
        new(VersionFor(TrustBoundaryLocality.InProcess), _ => null);

    public static FakeRecognizer Failing(GatewayError error) =>
        new(VersionFor(TrustBoundaryLocality.InProcess), _ => throw new ExtractionException(error));

    public static FakeRecognizer Named(string id, TrustBoundaryLocality locality) =>
        new(
            RecognizerVersion.Create(RecognizerId.Create(id), "1.0", locality),
            _ => RecognizedText.Create([RecognizedLine.Create("invoice 4471", 0.99, 1)]));

    public static RecognizerVersion VersionFor(TrustBoundaryLocality locality) =>
        RecognizerVersion.Create(RecognizerId.Create("fake-ocr"), "1.0", locality);

    public bool CanRecognize(string mediaType) =>
        string.Equals(mediaType, "image/png", StringComparison.Ordinal);

    public ValueTask<RecognizedText> RecognizeAsync(DocumentPart part, CancellationToken cancellationToken)
    {
        Calls++;

        return new ValueTask<RecognizedText>(_read(part)!);
    }
}

/// <summary>
/// Builds the parts the recognition tests read.
/// </summary>
internal static class RecognitionFixture
{
    public const string ImageMediaType = "image/png";

    public static DocumentPart Scan() =>
        DocumentPart.Create(
            PartId.Create("scan"),
            PartKind.Attachment,
            ImageMediaType,
            DocumentContent.FromBytes(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));

    public static async Task<IReadOnlyList<TextSegment>> ReadAsync(RecognizingExtractor extractor) =>
        await extractor.ExtractAsync(Scan(), CancellationToken.None);

    public static async Task<GatewayError> RefusedAsync(RecognizingExtractor extractor)
    {
        var exception = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(Scan(), CancellationToken.None));

        return exception.Error;
    }
}
