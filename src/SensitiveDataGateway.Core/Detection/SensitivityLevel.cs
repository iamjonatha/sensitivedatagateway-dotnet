namespace SensitiveDataGateway.Detection;

/// <summary>
/// How much damage disclosing a value of a given type would do.
/// </summary>
/// <remarks>
/// <para>
/// Sensitivity is a property of the <see cref="EntityType"/> under a given policy, not of the
/// individual value. The same email address is routine in one deployment and regulated in
/// another, and it is the policy that knows which.
/// </para>
/// <para>
/// The level drives what happens when detection is uncertain: an unresolved
/// <see cref="Critical"/> entity blocks the document, while an unresolved <see cref="Low"/>
/// one may simply be transformed defensively.
/// </para>
/// </remarks>
public enum SensitivityLevel
{
    /// <summary>
    /// Disclosure is undesirable but not harmful.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Disclosure would be a privacy incident of limited scope.
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// Disclosure would harm the person or the organization the value belongs to.
    /// </summary>
    High = 3,

    /// <summary>
    /// Disclosure is unacceptable under any circumstances.
    /// </summary>
    /// <remarks>
    /// Credentials belong here. A leaked API key is not a privacy incident that can be
    /// apologized for; it is an active compromise.
    /// </remarks>
    Critical = 4,
}
