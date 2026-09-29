using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Core.Tests;

public sealed class DocumentContentTests
{
    [Fact]
    public void FromText_ReportsTheEncodedSize()
    {
        var content = DocumentContent.FromText("abc");

        Assert.Equal(DocumentContentKind.Text, content.Kind);
        Assert.Equal(3, content.Length);
        Assert.Equal("abc", content.AsText());
    }

    [Fact]
    public void FromText_CountsBytesAndNotCharacters()
    {
        var content = DocumentContent.FromText("\u00e8");

        Assert.Equal(Encoding.UTF8.GetByteCount("\u00e8"), content.Length);
    }

    [Fact]
    public void FromText_AcceptsEmptyText()
    {
        var content = DocumentContent.FromText(string.Empty);

        Assert.Equal(0, content.Length);
    }

    [Fact]
    public void FromText_RejectsNull() => Assert.Throws<ArgumentNullException>(() => DocumentContent.FromText(null!));

    [Fact]
    public void AsBytes_ThrowsWhenTheContentIsText() =>
        Assert.Throws<InvalidOperationException>(() => DocumentContent.FromText("abc").AsBytes());

    [Fact]
    public void AsText_ThrowsWhenTheContentIsBytes() =>
        Assert.Throws<InvalidOperationException>(() =>
            DocumentContent.FromBytes(new byte[] { 1, 2, 3 }).AsText());

    [Fact]
    public async Task OpenStreamAsync_ThrowsWhenTheContentIsNotAStream() =>
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DocumentContent.FromText("abc").OpenStreamAsync(CancellationToken.None));

    [Fact]
    public async Task OpenStreamAsync_ReturnsWhatTheFactoryProduced()
    {
        var content = DocumentContent.FromStream(
            _ => ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            length: 3);

        await using var stream = await content.OpenStreamAsync(CancellationToken.None);

        Assert.Equal(3, stream.Length);
        Assert.Equal(3, content.Length);
    }

    [Fact]
    public void FromStream_AllowsAnUnknownSize()
    {
        var content = DocumentContent.FromStream(_ => ValueTask.FromResult<Stream>(Stream.Null), length: null);

        Assert.Null(content.Length);
    }

    [Fact]
    public void FromStream_RejectsANegativeSize() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DocumentContent.FromStream(_ => ValueTask.FromResult<Stream>(Stream.Null), length: -1));

    [Fact]
    public void ToString_DescribesTheContainerAndNotTheContent()
    {
        var content = DocumentContent.FromText("a secret");

        Assert.DoesNotContain("secret", content.ToString(), StringComparison.Ordinal);
    }
}

public sealed class DocumentMetadataTests
{
    [Fact]
    public void Empty_StatesNothing()
    {
        Assert.Null(DocumentMetadata.Empty.FileName);
        Assert.Null(DocumentMetadata.Empty.Language);
        Assert.Null(DocumentMetadata.Empty.Origin);
        Assert.Empty(DocumentMetadata.Empty.Properties);
    }

    [Fact]
    public void Create_TrimsTheValuesItWasGiven()
    {
        var metadata = DocumentMetadata.Create("  report.pdf ", " it-IT ", " mailbox ");

        Assert.Equal("report.pdf", metadata.FileName);
        Assert.Equal("it-IT", metadata.Language);
        Assert.Equal("mailbox", metadata.Origin);
    }

    [Fact]
    public void Create_RejectsAFileNameWithAControlCharacter() =>
        Assert.Throws<ArgumentException>(() => DocumentMetadata.Create("report\n.pdf", null, null));

    [Fact]
    public void Create_RejectsARepeatedPropertyName()
    {
        var properties = new List<KeyValuePair<string, string>>
        {
            new("sender", "a"),
            new("sender", "b"),
        };

        Assert.Throws<ArgumentException>(() => DocumentMetadata.Create(null, null, null, properties));
    }

    [Fact]
    public void Create_RejectsMorePropertiesThanTheMaximum()
    {
        var properties = Enumerable
            .Range(0, DocumentMetadata.MaxProperties + 1)
            .Select(index => new KeyValuePair<string, string>($"key{index}", "value"))
            .ToArray();

        Assert.Throws<ArgumentException>(() => DocumentMetadata.Create(null, null, null, properties));
    }

    [Fact]
    public void Create_AllowsAPropertyValueWithANewLine()
    {
        var properties = new[] { new KeyValuePair<string, string>("note", "first\nsecond") };

        var metadata = DocumentMetadata.Create(null, null, null, properties);

        Assert.Equal("first\nsecond", metadata.Properties["note"]);
    }

    [Fact]
    public void WithProperty_LeavesTheOriginalUnchanged()
    {
        var original = DocumentMetadata.Empty;

        var extended = original.WithProperty("sender", "value");

        Assert.Empty(original.Properties);
        Assert.Single(extended.Properties);
    }

    [Fact]
    public void WithProperty_ReplacesAValueThatIsAlreadyThere()
    {
        var metadata = DocumentMetadata.Empty
            .WithProperty("sender", "first")
            .WithProperty("sender", "second");

        Assert.Equal("second", metadata.Properties["sender"]);
    }

    [Fact]
    public void WithProperty_RejectsAValueLongerThanTheMaximum()
    {
        var value = new string('a', DocumentMetadata.MaxPropertyValueLength + 1);

        Assert.Throws<ArgumentException>(() => DocumentMetadata.Empty.WithProperty("note", value));
    }
}
