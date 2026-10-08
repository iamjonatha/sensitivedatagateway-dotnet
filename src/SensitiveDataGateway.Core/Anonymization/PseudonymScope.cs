namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// How far a pseudonym or a keyed digest stays correlatable.
/// </summary>
/// <remarks>
/// <para>
/// A scope answers one question: across how much material does the same token mean the same
/// thing? The wider the answer, the more an observer who holds several sanitized documents
/// can link them together, which is sometimes exactly the point and is never an accident.
/// </para>
/// <para>
/// The scope never widens the audience of the pseudonym map, which stays inside the trust
/// boundary in every case.
/// </para>
/// </remarks>
public enum PseudonymScope
{
    /// <summary>
    /// A token means one thing inside a single document and nothing outside it.
    /// </summary>
    Document = 1,

    /// <summary>
    /// A token means one thing across every part of one bundle, such as an email and its
    /// attachments, and nothing outside it.
    /// </summary>
    /// <remarks>
    /// This is the default. It is the narrowest scope under which a document set still reads
    /// coherently: the sender named in the body is the same token as the signatory in the
    /// attached contract.
    /// </remarks>
    Bundle = 2,

    /// <summary>
    /// A token means one thing across every operation of one session.
    /// </summary>
    Session = 3,

    /// <summary>
    /// A token means one thing across every operation of one tenant.
    /// </summary>
    /// <remarks>
    /// A tenant-wide token is a stable identifier for a person by another name. It is a
    /// deliberate choice that belongs in a retention policy, not a convenience.
    /// </remarks>
    Tenant = 4,
}
