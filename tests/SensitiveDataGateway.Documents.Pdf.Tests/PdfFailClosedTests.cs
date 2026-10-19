using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.Pdf.Tests;

/// <summary>
/// Proves that the extractor refuses rather than reads around anything it cannot account for.
/// </summary>
/// <remarks>
/// Each of these documents parses. What they have in common is that part of what they carry
/// would go unscanned, and a document scanned in part must not reach a provider. Every test
/// here asserts a failure; a regression that turns one of them into a successful read is a
/// regression that transmits unexamined content.
/// </remarks>
public sealed class PdfFailClosedTests
{
    [Fact]
    public async Task Refuses_a_document_that_needs_a_password()
    {
        var bytes = RawPdf.OnePage(
            extraObjects:
            [
                "<< /Filter /Standard /V 1 /R 2 /P -1 "
                + "/O <2222222222222222222222222222222222222222222222222222222222222222> "
                + "/U <3333333333333333333333333333333333333333333333333333333333333333> >>",
            ],
            trailerEntries: "/Encrypt 4 0 R /ID [<01> <01>]");

        var error = await Refused(bytes);

        Assert.Equal(ExtractionErrorCodes.PasswordProtected, error.Code);
    }

    [Fact]
    public async Task Refuses_a_document_carrying_a_file_inside_it()
    {
        var bytes = RawPdf.OnePage(
            catalogEntries: "/Names << /EmbeddedFiles << /Names [(payload.txt) 4 0 R] >> >>",
            extraObjects:
            [
                "<< /Type /Filespec /F (payload.txt) /EF << /F 5 0 R >> >>",
                RawPdf.Stream("/Type /EmbeddedFile", "IT60X0542811101000000123456"),
            ]);

        var error = await Refused(bytes);

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_a_form_whose_values_live_in_an_xml_stream()
    {
        var bytes = RawPdf.OnePage(
            catalogEntries: "/AcroForm << /Fields [] /XFA 4 0 R >>",
            extraObjects: [RawPdf.Stream("/Type /XFA", "<xdp></xdp>")]);

        var error = await Refused(bytes);

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_a_page_that_is_an_image_of_text()
    {
        var error = await Refused(PdfFixture.WithImageOnlyPage());

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
        Assert.Contains("1", error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_bytes_that_are_not_a_document()
    {
        var error = await Refused("this is not a PDF file"u8.ToArray());

        Assert.Equal(ExtractionErrorCodes.Malformed, error.Code);
    }

    [Fact]
    public async Task Refuses_a_document_that_was_truncated()
    {
        var bytes = PdfFixture.WithPages("Uno");
        var truncated = bytes[..(bytes.Length / 2)];

        var error = await Refused(truncated);

        Assert.Equal(ExtractionErrorCodes.Malformed, error.Code);
    }

    [Fact]
    public async Task Refuses_a_document_longer_than_the_page_bound()
    {
        var bytes = PdfFixture.WithPages("Uno", "Due", "Tre");
        var extractor = new PdfExtractor(PdfExtractorOptions.Default.WithMaxPageCount(2));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(PdfFixture.Part(bytes), CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, failure.Error.Code);
        Assert.Contains("2", failure.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_a_document_larger_than_the_byte_bound()
    {
        var bytes = LargerThanTheBound();
        var extractor = new PdfExtractor(PdfExtractorOptions.Default.WithMaxBytes(PdfExtractorOptions.MinimumBytes));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(PdfFixture.Part(bytes), CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, failure.Error.Code);
    }

    [Fact]
    public async Task Refuses_a_stream_larger_than_the_byte_bound_without_trusting_its_length()
    {
        var bytes = LargerThanTheBound();
        var part = DocumentPart.Create(
            PartId.Create("p1"),
            PartKind.Body,
            "application/pdf",
            DocumentContent.FromStream(
                _ => new ValueTask<System.IO.Stream>(new System.IO.MemoryStream(bytes, writable: false)),
                length: 1));

        var extractor = new PdfExtractor(PdfExtractorOptions.Default.WithMaxBytes(PdfExtractorOptions.MinimumBytes));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(part, CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, failure.Error.Code);
    }

    [Fact]
    public async Task Refuses_a_part_supplied_as_text_because_a_document_is_binary()
    {
        var part = DocumentPart.Create(
            PartId.Create("p1"),
            PartKind.Body,
            "application/pdf",
            DocumentContent.FromText("%PDF-1.7"));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await new PdfExtractor().ExtractAsync(part, CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.Malformed, failure.Error.Code);
    }

    /// <summary>
    /// Asserts that no message this package produces quotes the document.
    /// </summary>
    /// <returns>A task that completes when every refusal has been inspected.</returns>
    /// <remarks>
    /// The documents here all carry the same invented account number. A message repeating it
    /// would put document content into a log the moment the failure is reported, which is the
    /// one thing an error path in this library may never do.
    /// </remarks>
    [Fact]
    public async Task Never_quotes_the_document_in_a_message()
    {
        var secret = "IT60X0542811101000000123456";

        var embedded = RawPdf.OnePage(
            catalogEntries: "/Names << /EmbeddedFiles << /Names [(payload.txt) 4 0 R] >> >>",
            extraObjects:
            [
                $"<< /Type /Filespec /F ({secret}) /EF << /F 5 0 R >> >>",
                RawPdf.Stream("/Type /EmbeddedFile", secret),
            ]);

        foreach (var document in new[] { embedded, PdfFixture.WithImageOnlyPage() })
        {
            var error = await Refused(document);

            Assert.DoesNotContain(secret, error.Description, StringComparison.Ordinal);
        }
    }

    private static byte[] LargerThanTheBound()
    {
        var pages = new string[40];

        for (var index = 0; index < pages.Length; index++)
        {
            pages[index] = $"Pagina di riempimento numero {index}";
        }

        var bytes = PdfFixture.WithPages(pages);
        Assert.True(bytes.Length > PdfExtractorOptions.MinimumBytes);

        return bytes;
    }

    private static async Task<GatewayError> Refused(byte[] bytes)
    {
        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await PdfFixture.ReadAsync(bytes));

        return failure.Error;
    }
}
