using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SensitiveDataGateway;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Documents.Mime;
using SensitiveDataGateway.Documents.OpenXml;
using SensitiveDataGateway.Documents.Pdf;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Samples.EmailClassification;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

// An email is a bundle of parts, and an attachment is one of them. Nothing in this sample treats
// an attachment as a special case, because the library does not: the reader flattens the message,
// each part is offered to the extractors by media type, and a part nothing can read blocks the
// operation rather than passing as empty.
//
// The arrangement of the synthetic message is the point. The body carries one address. The bank
// details are in the PDF, the home address is in the Word document, and a second customer is in
// the workbook. A gateway that reads only what a mail client displays reports this message almost
// clean, and transmits the rest.

using ServiceProvider container = Walkthrough.Compose();

SanitizationPolicy policy = container.GetRequiredService<SanitizationPolicy>();
DocumentGateway gateway = container.GetRequiredService<DocumentGateway>();

Console.WriteLine("== The message ==");

DocumentBundle bundle = await Walkthrough.ReadAsync(SyntheticMessage.WithAttachments());

Walkthrough.Describe(bundle);

Console.WriteLine("== What is in there ==");

InspectionReport inspection = await gateway.InspectAsync(bundle, policy, CancellationToken.None);

Walkthrough.Describe(inspection);

Console.WriteLine("== Classification ==");

RecordingDecisionProvider<string, string> provider =
    RecordingDecisionProvider.Returning<string, string>("claim.accepted");

DocumentGateway<string, string> classifier = new(gateway, provider);

GatewayResult<string> decision = await classifier.ExecuteAsync(
    bundle,
    "classify",
    policy,
    CancellationToken.None);

Walkthrough.Describe(decision, provider);

Console.WriteLine("== Egress assertion ==");

// The assertion the library exists for, and the reason this sample has attachments at all. Four
// of these six values appear nowhere but inside an attachment, so a gateway that read only the
// body would fail here.
LeakAssert.NoneOf(provider, SyntheticMessage.DetectedValues());

Console.WriteLine("No detected value reached the provider, from the body or from an attachment.");
Console.WriteLine();

Console.WriteLine("== What this sample does not catch ==");

// The honest half of the demonstration. The postal address in the Word document is still in the
// payload printed above, because no deterministic detector can find an address written in prose.
Walkthrough.DescribeTheGap(provider);

Console.WriteLine("== An attachment nothing can read ==");

// The same message with one further attachment, in a format no extractor claims. The three
// readable attachments are unaffected, and the operation still blocks: a part that was not
// scanned is a part whose content nobody has accounted for.
DocumentBundle unreadable =
    await Walkthrough.ReadAsync(SyntheticMessage.WithAnUnreadableAttachment());

GatewayResult<string> refused = await classifier.ExecuteAsync(
    unreadable,
    "classify",
    policy,
    CancellationToken.None);

Walkthrough.DescribeRefusal(refused, provider);

return refused.IsAllowed ? 1 : 0;
