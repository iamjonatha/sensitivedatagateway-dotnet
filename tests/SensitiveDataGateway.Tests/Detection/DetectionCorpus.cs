using System.Collections.Generic;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// The annotated text the detectors are measured against.
/// </summary>
/// <remarks>
/// <para>
/// Every document here is invented, and so is every value in it. The account numbers, tax
/// codes and keys are synthetic values constructed to satisfy the relevant check digit; the
/// names, addresses and companies do not exist. Nothing in this file came from a real
/// document, and nothing in it may be replaced by anything that did.
/// </para>
/// <para>
/// The corpus is deliberately full of near misses — version numbers, invoice numbers, order
/// references, timestamps and identifiers that look like something they are not. A detector
/// set measured only against clean examples reports a precision it does not have.
/// </para>
/// <para>
/// This is not the corpus consumers get. <c>SensitiveDataGateway.Testing.SyntheticCorpus</c>
/// is shipped, and exists to prove that nothing crossed the boundary. This one stays here
/// and exists to measure precision and recall, which is why it is full of values that are
/// supposed to be left alone. Keeping them apart stops a near miss added for measurement
/// from quietly weakening a leak test.
/// </para>
/// </remarks>
internal static class DetectionCorpus
{
    /// <summary>
    /// A document and everything that should be found in it.
    /// </summary>
    /// <param name="Name">What the document is, for the failure message.</param>
    /// <param name="Text">The text to scan.</param>
    /// <param name="Expected">Every value a detector is expected to report.</param>
    internal sealed record Case(string Name, string Text, IReadOnlyList<Annotation> Expected);

    /// <summary>
    /// One value that should be found, and what it should be called.
    /// </summary>
    /// <param name="Type">The type the detector is expected to report.</param>
    /// <param name="Value">The exact text the reported span should cover.</param>
    internal sealed record Annotation(EntityType Type, string Value);

    /// <summary>
    /// Gets every case in the corpus.
    /// </summary>
    /// <returns>The cases.</returns>
    public static IReadOnlyList<Case> All() =>
    [
        new(
            "customer email",
            "Dear customer, we have updated the address on file to a.buyer@example.com. "
                + "Your order ORD-48201 ships on 2026-03-04.",
            [new(EntityType.Email, "a.buyer@example.com")]),
        new(
            "support ticket",
            "Ticket 4821 raised by b.user@mail.example.org, reachable on +39 02 1234 5678. "
                + "Severity 2, opened at 09:15:00, closed at 17:42:11.",
            [
                new(EntityType.Email, "b.user@mail.example.org"),
                new(EntityType.Phone, "+39 02 1234 5678"),
            ]),
        new(
            "payment instruction",
            "Please credit IT51X9999999999000000999999 for invoice 2026/0041, amount 1,250.00 EUR.",
            [new(EntityType.Iban, "IT51X9999999999000000999999")]),
        new(
            "card on file",
            "Card 4111 1111 1111 1111 expires 03/29. Reference 1234 5678 9012 3456 is not a card.",
            [new(EntityType.CreditCard, "4111 1111 1111 1111")]),
        new(
            "registration form",
            "Codice fiscale XQZXQZ80A01Z999G, document AB1234567, membership 998877.",
            [new(EntityType.TaxId, "XQZXQZ80A01Z999G")]),
        new(
            "deployment note",
            "Release 10.0.0 was rolled out at 23:59:59. See https://docs.example.com/release/10 "
                + "for the changelog. Build agent 192.0.2.17 ran the job.",
            [
                new(EntityType.Url, "https://docs.example.com/release/10"),
                new(EntityType.IpAddress, "192.0.2.17"),
            ]),
        new(
            "configuration file",
            "host=db.example.net\nport=5432\npassword=not-a-real-secret\npool_size=20",
            [new(EntityType.Password, "not-a-real-secret")]),
        new(
            "service credentials",
            "Server=db.example.net;Initial Catalog=billing;User Id=svc;Password=not-a-real-secret",
            [
                new(
                    EntityType.ConnectionString,
                    "Server=db.example.net;Initial Catalog=billing;User Id=svc;Password=not-a-real-secret"),
            ]),
        new(
            "api integration",
            "Set api_key = \"ZZZZZZZZZZZZZZZZZZZZZZZZ\" in the manifest, then restart. "
                + "The sandbox key AKIAZZZZZZZZZZZZZZZZ is rotated weekly.",
            [
                new(EntityType.ApiKey, "ZZZZZZZZZZZZZZZZZZZZZZZZ"),
                new(EntityType.ApiKey, "AKIAZZZZZZZZZZZZZZZZ"),
            ]),
        new(
            "bearer token",
            "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ6ZXJvIn0.ZmFrZS1zaWduYXR1cmUtdmFsdWU",
            [
                new(
                    EntityType.Jwt,
                    "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ6ZXJvIn0.ZmFrZS1zaWduYXR1cmUtdmFsdWU"),
            ]),
        new(
            "attached key",
            "The agent reads the key below.\n"
                + "-----BEGIN RSA PRIVATE KEY-----\nQUJDREVGRw==\n-----END RSA PRIVATE KEY-----\n"
                + "Rotate it after 2026-06-30.",
            [
                new(
                    EntityType.PrivateKey,
                    "-----BEGIN RSA PRIVATE KEY-----\nQUJDREVGRw==\n-----END RSA PRIVATE KEY-----"),
            ]),
        new(
            "prose with no values",
            "The claim was reviewed on the fourth of March and closed the same week. "
                + "Version 2.1 of the handbook applies, chapter 4, paragraph 12.",
            []),
        new(
            "numbers that are not identifiers",
            "Quantities 1000 2000 3000 4000, lot 5555-5555-5555-4445, ratio 16:9, build 4.8.1.1234.",
            []),
        new(
            "internal reference",
            "Order ORD-48201 for account 77-4821-9 was approved by workflow 12.",
            []),
    ];
}
