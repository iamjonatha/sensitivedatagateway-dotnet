using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// Builds the Word documents the tests read.
/// </summary>
internal static class DocxFixture
{
    /// <summary>
    /// Builds a document whose body holds the given paragraphs.
    /// </summary>
    /// <param name="paragraphs">One entry per paragraph.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithParagraphs(params string[] paragraphs) =>
        Build(main => main.Document = new Document(
            new Body(paragraphs.Select(ParagraphOf).Cast<OpenXmlElement>())));

    /// <summary>
    /// Builds a document whose body holds one table.
    /// </summary>
    /// <param name="rows">The cells, row by row.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithTable(params string[][] rows) =>
        Build(main => main.Document = new Document(
            new Body(new Table(rows.Select(RowOf).Cast<OpenXmlElement>()))));

    /// <summary>
    /// Builds a document with a header and a footer.
    /// </summary>
    /// <param name="header">The text of the header.</param>
    /// <param name="footer">The text of the footer.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithHeaderAndFooter(string header, string footer) =>
        Build(main =>
        {
            main.Document = new Document(new Body(ParagraphOf("Body")));
            main.AddNewPart<HeaderPart>("rIdHeader").Header = new Header(ParagraphOf(header));
            main.AddNewPart<FooterPart>("rIdFooter").Footer = new Footer(ParagraphOf(footer));
        });

    /// <summary>
    /// Builds a document carrying one comment.
    /// </summary>
    /// <param name="author">The name on the comment.</param>
    /// <param name="text">The text of the comment.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithComment(string author, string text) =>
        Build(main =>
        {
            main.Document = new Document(new Body(ParagraphOf("Body")));

            var comment = new Comment(ParagraphOf(text))
            {
                Id = "1",
                Author = author,
            };

            main.AddNewPart<WordprocessingCommentsPart>("rIdComments").Comments =
                new Comments(comment);
        });

    /// <summary>
    /// Builds a document carrying one footnote.
    /// </summary>
    /// <param name="text">The text of the footnote.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithFootnote(string text) =>
        Build(main =>
        {
            main.Document = new Document(new Body(ParagraphOf("Body")));

            var footnote = new Footnote(ParagraphOf(text)) { Id = 2 };

            main.AddNewPart<FootnotesPart>("rIdFootnotes").Footnotes = new Footnotes(footnote);
        });

    /// <summary>
    /// Builds a document with an external link anchored to a run.
    /// </summary>
    /// <param name="uri">The target of the link.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithHyperlink(string uri) =>
        Build(main =>
        {
            main.Document = new Document(new Body(ParagraphOf("Contact")));
            main.AddHyperlinkRelationship(new Uri(uri), isExternal: true, "rIdLink");
        });

    /// <summary>
    /// Builds a document whose body holds text a reviewer deleted.
    /// </summary>
    /// <param name="kept">The text that stayed.</param>
    /// <param name="deleted">The text that was struck out but is still in the file.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithTrackedDeletion(string kept, string deleted) =>
        Build(main => main.Document = new Document(
            new Body(
                new Paragraph(
                    new Run(new Text(kept)),
                    new DeletedRun(new Run(new DeletedText(deleted)))))));

    /// <summary>
    /// Builds a document with entries in its package properties.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="creator">The creator.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithProperties(string title, string creator) =>
        Build(
            main => main.Document = new Document(new Body(ParagraphOf("Body"))),
            package =>
            {
                package.PackageProperties.Title = title;
                package.PackageProperties.Creator = creator;
            });

    /// <summary>
    /// Builds a document that imports a chunk of HTML.
    /// </summary>
    /// <param name="html">The markup of the chunk.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithAltChunk(string html) =>
        Build(main =>
        {
            main.Document = new Document(
                new Body(ParagraphOf("Body"), new AltChunk { Id = "rIdChunk" }));

            var chunk = main.AddAlternativeFormatImportPart(
                AlternativeFormatImportPartType.Html,
                "rIdChunk");

            using var stream = chunk.GetStream(FileMode.Create);
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);

            stream.Write(bytes, 0, bytes.Length);
        });

    /// <summary>
    /// Builds a master document that names a separate file.
    /// </summary>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithSubDocument() =>
        Build(main =>
        {
            main.Document = new Document(
                new Body(ParagraphOf("Body"), new SubDocumentReference { Id = "rIdSub" }));

            main.AddExternalRelationship(
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/subDocument",
                new Uri("https://example.invalid/chapter-two.docx"),
                "rIdSub");
        });

    private static Paragraph ParagraphOf(string text) => new(new Run(new Text(text)));

    private static TableRow RowOf(string[] cells) =>
        new(cells.Select(cell => new TableCell(ParagraphOf(cell))).Cast<OpenXmlElement>());

    private static byte[] Build(Action<MainDocumentPart> fill) => Build(fill, _ => { });

    private static byte[] Build(
        Action<MainDocumentPart> fill,
        Action<WordprocessingDocument> describe)
    {
        using var buffer = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
            buffer,
            WordprocessingDocumentType.Document))
        {
            fill(document.AddMainDocumentPart());
            describe(document);
        }

        return buffer.ToArray();
    }
}
