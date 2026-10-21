using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using MimeKit;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Document = DocumentFormat.OpenXml.Wordprocessing.Document;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace SensitiveDataGateway.Samples.EmailClassification;

/// <summary>
/// Writes the message this sample classifies, and the attachments inside it.
/// </summary>
/// <remarks>
/// <para>
/// Every value here is synthetic. The identifiers are structurally valid so that the detectors
/// recognise them, and they belong to no one.
/// </para>
/// <para>
/// The interesting arrangement is where each value sits. The body of the message carries one
/// address and nothing else. The bank details are in the PDF, the home address is in the Word
/// document, and the customer list is in the workbook. A gateway that reads only the body
/// reports this message almost clean.
/// </para>
/// </remarks>
internal static class SyntheticMessage
{
    /// <summary>The address in the body of the message.</summary>
    public const string SenderAddress = "olivia.hartley@example.com";

    /// <summary>The address in the recipient header.</summary>
    public const string RecipientAddress = "claims@example.org";

    /// <summary>The account number, which appears only inside the PDF.</summary>
    public const string ContractIban = "GB71NWBK99999999999999";

    /// <summary>The telephone number, which appears only inside the PDF.</summary>
    public const string ContractPhone = "+44 20 7946 0999";

    /// <summary>The address, which appears only inside the Word document.</summary>
    public const string StatementAddress = "14 Example Street, London EX1 2AM";

    /// <summary>The second account number, which appears only inside the workbook.</summary>
    public const string CustomerIban = "DE02500105170137075030";

    /// <summary>The second address, which appears only inside the workbook.</summary>
    public const string CustomerEmail = "noah.bergmann@example.net";

    /// <summary>The media type of a PDF attachment.</summary>
    public const string PdfMediaType = "application/pdf";

    /// <summary>The media type of a Word attachment.</summary>
    public const string DocxMediaType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>The media type of an Excel attachment.</summary>
    public const string XlsxMediaType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// The values the deterministic detectors find, wherever they sit.
    /// </summary>
    /// <returns>The values.</returns>
    public static IReadOnlyList<string> DetectedValues() =>
    [
        SenderAddress,
        RecipientAddress,
        ContractIban,
        ContractPhone,
        CustomerIban,
        CustomerEmail,
    ];

    /// <summary>
    /// The value no deterministic detector finds.
    /// </summary>
    /// <returns>The values.</returns>
    /// <remarks>
    /// A postal address written in prose has no checksum, no prefix and no fixed shape, so
    /// there is no pattern to match it with and no arrangement of the shipped detectors that
    /// would. It is the case semantic detection exists for, and until that ships this sample
    /// transmits it. Saying so is the point: a sample that quietly left the address out would
    /// report a guarantee the library does not make.
    /// </remarks>
    public static IReadOnlyList<string> UndetectedValues() => [StatementAddress];

    /// <summary>
    /// Writes the message, with the three attachments an office sends.
    /// </summary>
    /// <returns>The bytes of the message, in RFC 5322 form.</returns>
    public static byte[] WithAttachments() => Write(body =>
    {
        Attach(body, "contract.pdf", PdfMediaType, Contract());
        Attach(body, "statement.docx", DocxMediaType, Statement());
        Attach(body, "customers.xlsx", XlsxMediaType, Customers());
    });

    /// <summary>
    /// Writes the same message with one further attachment that no extractor reads.
    /// </summary>
    /// <returns>The bytes of the message, in RFC 5322 form.</returns>
    public static byte[] WithAnUnreadableAttachment() => Write(body =>
    {
        Attach(body, "contract.pdf", PdfMediaType, Contract());
        Attach(body, "statement.docx", DocxMediaType, Statement());
        Attach(body, "customers.xlsx", XlsxMediaType, Customers());
        Attach(body, "archive.zip", "application/zip", [0x50, 0x4B, 0x03, 0x04]);
    });

    private static byte[] Write(Action<BodyBuilder> attach)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress("Olivia Hartley", SenderAddress));
        message.To.Add(new MailboxAddress("Claims", RecipientAddress));
        message.Subject = "Claim 4471 — documents attached";

        var body = new BodyBuilder
        {
            TextBody =
                "Good morning,\n\n" +
                "the documents for claim 4471 are attached. Please confirm receipt at " +
                $"{SenderAddress}.\n\nKind regards,\nOlivia",
        };

        attach(body);
        message.Body = body.ToMessageBody();

        using var buffer = new MemoryStream();
        message.WriteTo(buffer);

        return buffer.ToArray();
    }

    private static void Attach(BodyBuilder body, string name, string mediaType, byte[] content)
    {
        var type = ContentType.Parse(mediaType);
        body.Attachments.Add(name, content, type);
    }

    private static byte[] Contract()
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);

        page.AddText("Settlement instruction for claim 4471", 12, new PdfPoint(50, 720), font);
        page.AddText($"Account: {ContractIban}", 12, new PdfPoint(50, 700), font);
        page.AddText($"Telephone: {ContractPhone}", 12, new PdfPoint(50, 680), font);

        return builder.Build();
    }

    private static byte[] Statement()
    {
        using var buffer = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
            buffer,
            WordprocessingDocumentType.Document))
        {
            document.AddMainDocumentPart().Document = new Document(
                new Body(
                    Paragraph("Statement of loss"),
                    Paragraph($"Residence: {StatementAddress}"),
                    Paragraph("The claimant reports the loss occurred at the above address.")));
        }

        return buffer.ToArray();
    }

    private static byte[] Customers()
    {
        using var buffer = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(
            buffer,
            SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            var data = new SheetData(
                Row("Name", "Account", "Contact"),
                Row("Noah Bergmann", CustomerIban, CustomerEmail));

            workbook.Workbook = new Workbook(
                new Sheets(new Sheet { Id = "rIdSheet", SheetId = 1, Name = "Customers" }));
            workbook.AddNewPart<WorksheetPart>("rIdSheet").Worksheet = new Worksheet(data);
        }

        return buffer.ToArray();
    }

    private static Paragraph Paragraph(string text) => new(new Run(new Text(text)));

    private static Row Row(params string[] cells) =>
        new(cells.Select(Cell).Cast<OpenXmlElement>());

    private static Cell Cell(string text) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(text)),
    };

    /// <summary>
    /// Formats a count for the console without depending on the ambient culture.
    /// </summary>
    /// <param name="value">The count.</param>
    /// <returns>The text.</returns>
    public static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
