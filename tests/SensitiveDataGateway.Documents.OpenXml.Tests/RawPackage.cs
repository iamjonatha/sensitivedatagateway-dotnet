using System.IO;
using System.IO.Compression;
using System.Text;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// Builds Open XML packages byte by byte, including ones no word processor would write.
/// </summary>
/// <remarks>
/// The SDK will not produce a document carrying a document type declaration, and that is
/// exactly the document the extractor has to refuse. Anything hostile is assembled here, as
/// a zip archive with the parts spelled out.
/// </remarks>
internal static class RawPackage
{
    private const string ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string OfficeDocumentRelationship =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";

    private const string WordprocessingContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    /// <summary>
    /// Builds a Word package whose main part holds the given markup verbatim.
    /// </summary>
    /// <param name="documentXml">The markup of <c>word/document.xml</c>.</param>
    /// <returns>The bytes of the package.</returns>
    public static byte[] Wordprocessing(string documentXml)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes());
            Write(archive, "_rels/.rels", RootRelationships());
            Write(archive, "word/document.xml", documentXml);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Builds a Word package whose body holds the given markup, preceded by a declaration.
    /// </summary>
    /// <param name="declaration">The document type declaration.</param>
    /// <param name="body">The markup of the body.</param>
    /// <returns>The bytes of the package.</returns>
    public static byte[] WithDoctype(string declaration, string body) =>
        Wordprocessing(
            $"""
             <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
             {declaration}
             <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
               <w:body>{body}</w:body>
             </w:document>
             """);

    /// <summary>
    /// Builds a zip archive holding no parts at all.
    /// </summary>
    /// <returns>The bytes of the archive.</returns>
    public static byte[] EmptyArchive()
    {
        using var buffer = new MemoryStream();

        using (var _ = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Builds the opening bytes of a compound file, which is what encryption produces.
    /// </summary>
    /// <returns>The bytes.</returns>
    /// <remarks>
    /// An Open XML document protected with a password is not an encrypted zip archive. It is
    /// a compound file whose streams hold the encrypted package, and it announces itself with
    /// the signature written here.
    /// </remarks>
    public static byte[] CompoundFile()
    {
        var bytes = new byte[512];
        byte[] signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

        signature.CopyTo(bytes, 0);

        return bytes;
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        using var stream = archive.CreateEntry(path).Open();
        var bytes = Encoding.UTF8.GetBytes(content);

        stream.Write(bytes, 0, bytes.Length);
    }

    private static string ContentTypes() =>
        $"""
         <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
         <Types xmlns="{ContentTypesNamespace}">
           <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
           <Default Extension="xml" ContentType="application/xml" />
           <Override PartName="/word/document.xml" ContentType="{WordprocessingContentType}" />
         </Types>
         """;

    private static string RootRelationships() =>
        $"""
         <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
         <Relationships xmlns="{RelationshipsNamespace}">
           <Relationship Id="rId1" Type="{OfficeDocumentRelationship}" Target="word/document.xml" />
         </Relationships>
         """;
}
