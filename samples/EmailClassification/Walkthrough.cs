using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Documents.Mime;
using SensitiveDataGateway.Documents.OpenXml;
using SensitiveDataGateway.Documents.Pdf;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Samples.EmailClassification;

/// <summary>
/// The composition and the printing, kept out of the way so that the program reads as the
/// sequence of steps it is.
/// </summary>
internal static class Walkthrough
{
    /// <summary>
    /// Composes a gateway that reads mail, PDF, Word and Excel.
    /// </summary>
    /// <returns>The container.</returns>
    /// <remarks>
    /// Registering the three format adapters is the whole of what it takes to read attachments.
    /// There is no attachment-specific switch: an attachment is a part, and a part is offered to
    /// the extractors by media type like any other.
    /// </remarks>
    public static ServiceProvider Compose()
    {
        ServiceCollection services = new();

        services.AddSensitiveDataGateway(gateway => gateway
            .WithPolicy(Policy())
            // Registering any extractor replaces the default set, so the plain text one has to
            // be named here. The builder does not merge: an adapter list that silently gained a
            // reader nobody asked for would be the wrong surprise in this library.
            .WithExtractor(new PlainTextExtractor())
            .WithExtractor(new MimeStructureExtractor())
            .WithExtractor(new PdfExtractor())
            .WithExtractor(new DocxExtractor())
            .WithExtractor(new XlsxExtractor())
            // A placeholder. A real deployment reads the key from a secret store, never from
            // source, and rotating it changes every pseudonym it ever produced.
            .WithHashingKey(HashingKey.Create(
                Encoding.UTF8.GetBytes("not-a-real-key-not-a-real-key-not-a-real-key"))));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Reads a message into a bundle.
    /// </summary>
    /// <param name="message">The bytes of the message.</param>
    /// <returns>The bundle.</returns>
    public static async Task<DocumentBundle> ReadAsync(byte[] message)
    {
        using MemoryStream stream = new(message);

        return await new MimeBundleReader().ReadAsync(
            BundleId.New(),
            stream,
            ProcessingLimits.Default,
            CancellationToken.None);
    }

    /// <summary>
    /// Prints the parts the reader produced.
    /// </summary>
    /// <param name="bundle">The bundle.</param>
    public static void Describe(DocumentBundle bundle)
    {
        Console.WriteLine($"Parts: {Count(bundle.Attachments.Count + 1)}");

        foreach (DocumentPart part in bundle.Parts)
        {
            string name = part.Metadata.FileName ?? "(the message itself)";
            Console.WriteLine($"  {part.Kind,-10} {part.MediaType,-72} {name}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints what the inspection found, without printing anything it found.
    /// </summary>
    /// <param name="inspection">The report.</param>
    public static void Describe(InspectionReport inspection)
    {
        Console.WriteLine($"Segments: {Count(inspection.SegmentCount)}");
        Console.WriteLine($"Characters: {inspection.TotalCharacters.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Extraction failures: {Count(inspection.ExtractionFailureCount)}");
        Console.WriteLine($"Would be allowed: {inspection.WouldBeAllowed}");

        foreach (KeyValuePair<EntityType, int> entry in inspection.EntityCounts)
        {
            Console.WriteLine($"  {entry.Key}: {Count(entry.Value)}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints the decision, and what the provider was handed to reach it.
    /// </summary>
    /// <param name="decision">The result.</param>
    /// <param name="provider">The provider that recorded the call.</param>
    public static void Describe(
        GatewayResult<string> decision,
        RecordingDecisionProvider<string, string> provider)
    {
        Console.WriteLine($"Allowed: {decision.IsAllowed}");
        Console.WriteLine($"Decision: {decision.Value ?? "(none)"}");

        ValidatedPayload payload = provider.LastPayload
            ?? throw new InvalidOperationException("The provider was expected to be called.");

        Console.WriteLine($"Payload segments: {Count(payload.Content.Count)}");
        Console.WriteLine($"Payload hash: {payload.ContentHash}");
        Console.WriteLine();
        Console.WriteLine("What the provider actually received:");

        foreach (SanitizedSegment segment in payload.Content)
        {
            Console.WriteLine($"  [{segment.PartId}] {segment.Text}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Prints what the deterministic detectors could not find, and did not transform.
    /// </summary>
    /// <param name="provider">The provider that recorded the call.</param>
    /// <remarks>
    /// This check is deliberately the wrong way round: it asserts that the gap is still open.
    /// When semantic detection ships, it fails, and the sample has to be told that the library
    /// now makes a promise it did not make before.
    /// </remarks>
    public static void DescribeTheGap(RecordingDecisionProvider<string, string> provider)
    {
        ValidatedPayload payload = provider.LastPayload
            ?? throw new InvalidOperationException("The provider was expected to be called.");

        bool survived = SyntheticMessage.UndetectedValues().Any(
            value => payload.Content.Any(
                segment => segment.Text.Contains(value, StringComparison.Ordinal)));

        if (survived)
        {
            Console.WriteLine("The postal address in the Word document survived, as expected.");
            Console.WriteLine("It has no checksum, no prefix and no fixed shape, so there is no");
            Console.WriteLine("pattern that matches it and no arrangement of the shipped");
            Console.WriteLine("detectors that would. The gateway transmitted it.");
        }
        else
        {
            Console.WriteLine("The postal address was transformed. Update this sample: the");
            Console.WriteLine("library now detects something it could not when this was written.");
        }

        Console.WriteLine();
        Console.WriteLine("A pattern finds what has a shape. Everything else needs a detector");
        Console.WriteLine("that reads, and a detector that reads must not be reached over the");
        Console.WriteLine("network with raw content. That is the next thing this library builds.");
        Console.WriteLine();
    }

    /// <summary>
    /// Prints a refusal, which is a result rather than an exception.
    /// </summary>
    /// <param name="refused">The result.</param>
    /// <param name="provider">The provider that was not called again.</param>
    public static void DescribeRefusal(
        GatewayResult<string> refused,
        RecordingDecisionProvider<string, string> provider)
    {
        Console.WriteLine($"Allowed: {refused.IsAllowed}");
        Console.WriteLine($"Provider calls so far: {Count(provider.Calls.Count)}");

        foreach (GatewayError error in refused.Report.ToErrors())
        {
            Console.WriteLine($"  {error.Code}: {error.Description}");
        }

        Console.WriteLine();
        Console.WriteLine(refused.IsAllowed
            ? "A part nothing read reached a provider. This sample is meant to fail here."
            : "The zip was not scanned, so the message was refused rather than partly sent.");
    }

    private static SanitizationPolicy Policy() =>
        SanitizationPolicy.CreateBuilder(
                PolicyId.Create("sample.email-classification"),
                PolicyVersion.Create("1.0.0"))
            .WithDefaultAction(TransformationAction.Pseudonymize)
            .WithUnknownEntityAction(TransformationAction.Redact)
            .WithDefaultThresholds(ConfidenceThresholds.Create(0.70, 0.50))
            .WithDefaultScope(PseudonymScope.Bundle)
            // A claims classifier needs to tell two accounts apart and never needs to read
            // either, so an account number becomes a stable pseudonym. An address is not even
            // that useful to the decision, so it goes.
            .AddRule(PolicyRule.For(EntityType.Iban, TransformationAction.Pseudonymize))
            .AddRule(PolicyRule.For(EntityType.Email, TransformationAction.Redact))
            .AddRule(PolicyRule.For(EntityType.Phone, TransformationAction.Redact))
            .Build();

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
