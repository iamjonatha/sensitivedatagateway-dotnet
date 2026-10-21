using System;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// Proves that the Open XML extractors refuse rather than read around what they cannot
/// account for.
/// </summary>
/// <remarks>
/// Most of these packages parse. What they have in common is that part of what they carry
/// would go unscanned, and a document scanned in part must not reach a provider. Every test
/// here asserts a failure; a regression that turns one of them into a successful read is a
/// regression that transmits unexamined content.
/// </remarks>
public sealed class OpenXmlFailClosedTests
{
    private const string Secret = "IT60X0542811101000000123456";

    /// <summary>
    /// Proves that an entity declaration is refused rather than resolved.
    /// </summary>
    /// <param name="declaration">The document type declaration.</param>
    /// <returns>A task that completes when the package has been refused.</returns>
    /// <remarks>
    /// This is the XXE case. A reader that resolves an external entity turns a document into
    /// a request for a local file or a remote URL, which is both a read of something nobody
    /// asked for and a connection from a library that must open none. The SDK prohibits
    /// document type declarations outright; this test is here so that a change of reader, or
    /// of its settings, cannot quietly take the prohibition away.
    /// </remarks>
    [Theory]
    [InlineData("<!DOCTYPE w:document [<!ENTITY secret \"IT60X0542811101000000123456\">]>")]
    [InlineData("<!DOCTYPE w:document [<!ENTITY secret SYSTEM \"file:///C:/Windows/win.ini\">]>")]
    [InlineData("<!DOCTYPE w:document SYSTEM \"https://example.invalid/evil.dtd\">")]
    public async Task Refuses_a_document_that_declares_an_entity(string declaration)
    {
        var bytes = RawPackage.WithDoctype(
            declaration,
            "<w:p><w:r><w:t>&secret;</w:t></w:r></w:p>");

        var error = await OpenXmlFixture.RefusedDocxAsync(bytes);

        Assert.Equal(ExtractionErrorCodes.Malformed, error.Code);
    }

    /// <summary>
    /// Proves that a document importing another format is refused.
    /// </summary>
    /// <returns>A task that completes when the document has been refused.</returns>
    /// <remarks>
    /// An <c>altChunk</c> carries HTML, RTF or another document format that the word
    /// processor imports when it opens the file. Its text is part of what a reader sees and
    /// none of it is in the Word markup.
    /// </remarks>
    [Fact]
    public async Task Refuses_a_document_that_imports_a_chunk_in_another_format()
    {
        var bytes = DocxFixture.WithAltChunk($"<html><body>{Secret}</body></html>");

        var error = await OpenXmlFixture.RefusedDocxAsync(bytes);

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_a_document_assembled_from_separate_files()
    {
        var error = await OpenXmlFixture.RefusedDocxAsync(DocxFixture.WithSubDocument());

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_a_workbook_whose_values_come_from_another_file()
    {
        var error = await OpenXmlFixture.RefusedXlsxAsync(XlsxFixture.WithExternalWorkbook());

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    /// <summary>
    /// Proves that an encrypted document is told apart from a damaged one.
    /// </summary>
    /// <returns>A task that completes when the document has been refused.</returns>
    /// <remarks>
    /// An encrypted Open XML file is a compound file holding the encrypted package, and the
    /// package reader reports it as corrupted data. Reporting it as malformed would send an
    /// operator looking for a broken file instead of a password.
    /// </remarks>
    [Fact]
    public async Task Refuses_an_encrypted_document_as_needing_a_password()
    {
        var error = await OpenXmlFixture.RefusedDocxAsync(RawPackage.CompoundFile());

        Assert.Equal(ExtractionErrorCodes.PasswordProtected, error.Code);
    }

    [Fact]
    public async Task Refuses_an_encrypted_workbook_as_needing_a_password()
    {
        var error = await OpenXmlFixture.RefusedXlsxAsync(RawPackage.CompoundFile());

        Assert.Equal(ExtractionErrorCodes.PasswordProtected, error.Code);
    }

    /// <summary>
    /// Proves that a package with no main part is refused.
    /// </summary>
    /// <returns>A task that completes when the package has been refused.</returns>
    /// <remarks>
    /// A zip archive holding no parts opens without complaint and yields no text. Returning
    /// no segments for it would report a package nothing read as a package with nothing in
    /// it.
    /// </remarks>
    [Fact]
    public async Task Refuses_an_archive_holding_no_parts()
    {
        var error = await OpenXmlFixture.RefusedDocxAsync(RawPackage.EmptyArchive());

        Assert.Equal(ExtractionErrorCodes.Malformed, error.Code);
    }

    [Fact]
    public async Task Refuses_bytes_that_are_not_a_package()
    {
        var error = await OpenXmlFixture.RefusedDocxAsync("this is not a document"u8.ToArray());

        Assert.Equal(ExtractionErrorCodes.Malformed, error.Code);
    }

    [Fact]
    public async Task Refuses_a_package_that_was_truncated()
    {
        var bytes = DocxFixture.WithParagraphs("Uno");

        var error = await OpenXmlFixture.RefusedDocxAsync(bytes[..(bytes.Length / 2)]);

        Assert.Equal(ExtractionErrorCodes.Malformed, error.Code);
    }

    [Fact]
    public async Task Refuses_a_document_larger_than_the_byte_bound()
    {
        var bytes = DocxFixture.WithParagraphs("Uno");
        Assert.True(bytes.Length > OpenXmlExtractorOptions.MinimumBytes);

        var extractor = new DocxExtractor(
            OpenXmlExtractorOptions.Default.WithMaxBytes(OpenXmlExtractorOptions.MinimumBytes));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(
                OpenXmlFixture.Part(bytes, OpenXmlFixture.WordMediaType),
                CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, failure.Error.Code);
    }

    /// <summary>
    /// Proves that the declared length of a stream is not taken on trust.
    /// </summary>
    /// <returns>A task that completes when the document has been refused.</returns>
    /// <remarks>
    /// The length comes from whoever built the part. A bound enforced on the declared length
    /// rather than on the bytes actually read is a bound that a wrong number turns off.
    /// </remarks>
    [Fact]
    public async Task Refuses_a_stream_larger_than_the_byte_bound_without_trusting_its_length()
    {
        var bytes = DocxFixture.WithParagraphs("Uno");

        var part = DocumentPart.Create(
            PartId.Create("p1"),
            PartKind.Body,
            OpenXmlFixture.WordMediaType,
            DocumentContent.FromStream(
                _ => new ValueTask<System.IO.Stream>(new System.IO.MemoryStream(bytes, writable: false)),
                length: 1));

        var extractor = new DocxExtractor(
            OpenXmlExtractorOptions.Default.WithMaxBytes(OpenXmlExtractorOptions.MinimumBytes));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(part, CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, failure.Error.Code);
    }

    [Fact]
    public async Task Refuses_a_document_yielding_more_segments_than_the_bound_allows()
    {
        var bytes = DocxFixture.WithParagraphs("Uno", "Due", "Tre");

        var extractor = new DocxExtractor(
            OpenXmlExtractorOptions.Default.WithMaxSegmentCount(2));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await extractor.ExtractAsync(
                OpenXmlFixture.Part(bytes, OpenXmlFixture.WordMediaType),
                CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, failure.Error.Code);
        Assert.Contains("2", failure.Error.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_a_part_supplied_as_text_because_a_package_is_binary()
    {
        var part = DocumentPart.Create(
            PartId.Create("p1"),
            PartKind.Body,
            OpenXmlFixture.WordMediaType,
            DocumentContent.FromText("<w:document />"));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await new DocxExtractor().ExtractAsync(part, CancellationToken.None));

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
        var documents = new[]
        {
            DocxFixture.WithAltChunk($"<html><body>{Secret}</body></html>"),
            RawPackage.WithDoctype(
                $"<!DOCTYPE w:document [<!ENTITY secret \"{Secret}\">]>",
                $"<w:p><w:r><w:t>{Secret}</w:t></w:r></w:p>"),
            System.Text.Encoding.UTF8.GetBytes(Secret),
        };

        foreach (var document in documents)
        {
            var error = await OpenXmlFixture.RefusedDocxAsync(document);

            Assert.DoesNotContain(Secret, error.Description, StringComparison.Ordinal);
        }
    }
}
