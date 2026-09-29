using System;
using System.Collections.Generic;
using System.Linq;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Core.Tests;

public sealed class DocumentBundleTests
{
    [Fact]
    public void Create_PutsThePrimaryDocumentFirst()
    {
        var primary = Part("part-1", PartKind.Body);
        var attachment = Part("part-2", PartKind.Attachment);

        var bundle = DocumentBundle.Create(BundleId.New(), primary, [attachment]);

        Assert.Equal([primary, attachment], bundle.Parts);
    }

    [Fact]
    public void Create_CopiesTheAttachments()
    {
        var attachments = new List<DocumentPart> { Part("part-2", PartKind.Attachment) };
        var bundle = DocumentBundle.Create(BundleId.New(), Part("part-1", PartKind.Body), attachments);

        attachments.Add(Part("part-3", PartKind.Attachment));

        Assert.Single(bundle.Attachments);
    }

    [Fact]
    public void Create_RejectsADefaultIdentifier() =>
        Assert.Throws<ArgumentException>(() =>
            DocumentBundle.Create(default, Part("part-1", PartKind.Body)));

    [Fact]
    public void Create_RejectsTwoPartsThatShareAnIdentifier()
    {
        var primary = Part("part-1", PartKind.Body);
        var attachment = Part("part-1", PartKind.Attachment);

        Assert.Throws<ArgumentException>(() =>
            DocumentBundle.Create(BundleId.New(), primary, [attachment]));
    }

    [Fact]
    public void Create_RejectsAPartWhoseParentIsNotInTheBundle()
    {
        var primary = Part("part-1", PartKind.Body);
        var orphan = DocumentPart.Create(
            PartId.Create("part-2"),
            PartKind.Embedded,
            "text/plain",
            DocumentContent.FromText(string.Empty),
            PartId.Create("part-9"),
            DocumentMetadata.Empty);

        Assert.Throws<ArgumentException>(() =>
            DocumentBundle.Create(BundleId.New(), primary, [orphan]));
    }

    [Fact]
    public void Create_AcceptsAnAttachmentNestedInAnotherAttachment()
    {
        var primary = Part("part-1", PartKind.Body);
        var attachment = Part("part-2", PartKind.Attachment);
        var nested = DocumentPart.Create(
            PartId.Create("part-3"),
            PartKind.Embedded,
            "text/plain",
            DocumentContent.FromText(string.Empty),
            attachment.Id,
            DocumentMetadata.Empty);

        var bundle = DocumentBundle.Create(BundleId.New(), primary, [attachment, nested]);

        Assert.Equal(3, bundle.Parts.Count());
    }

    [Fact]
    public void Create_RejectsAPrimaryDocumentThatIsNested()
    {
        var primary = DocumentPart.Create(
            PartId.Create("part-1"),
            PartKind.Body,
            "text/plain",
            DocumentContent.FromText(string.Empty),
            PartId.Create("part-2"),
            DocumentMetadata.Empty);

        Assert.Throws<ArgumentException>(() => DocumentBundle.Create(BundleId.New(), primary));
    }

    [Fact]
    public void ToString_CountsThePartsAndQuotesNothing()
    {
        var bundle = DocumentBundle.Create(
            BundleId.Create("case-1"),
            Part("part-1", PartKind.Body),
            [Part("part-2", PartKind.Attachment)]);

        Assert.Equal("case-1 (2 parts)", bundle.ToString());
    }

    private static DocumentPart Part(string id, PartKind kind) =>
        DocumentPart.Create(
            PartId.Create(id),
            kind,
            "text/plain",
            DocumentContent.FromText(string.Empty));
}
