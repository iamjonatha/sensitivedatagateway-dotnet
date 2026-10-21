namespace SensitiveDataGateway;

/// <summary>
/// Where a component that reads raw content runs, relative to the trust boundary.
/// </summary>
/// <remarks>
/// <para>
/// This is the only property of such a component the library reasons about, because it is the
/// only one that changes what the gateway can promise. An engine that runs in the process
/// cannot leak a document; an engine behind a vendor's API has already received it, in full,
/// before any decision about it has been made.
/// </para>
/// <para>
/// The same question is asked of a text recognizer and of a semantic detector, and it has the
/// same three answers, so it is asked once. A component that reads content before the
/// validator has seen it is inside this classification whatever stage it belongs to.
/// </para>
/// <para>
/// The locality is declared by the implementation and taken at its word. The library cannot
/// prove that an engine stays local, and does not pretend to: it records what the
/// implementation claims, refuses the remote case unless the application asked for it in a
/// call that is visible in a diff, and leaves the verification where it belongs, with the
/// person reviewing the dependency.
/// </para>
/// </remarks>
public enum TrustBoundaryLocality
{
    /// <summary>
    /// The component runs inside the calling process.
    /// </summary>
    InProcess = 1,

    /// <summary>
    /// The component runs elsewhere in the deployment the application controls.
    /// </summary>
    /// <remarks>
    /// A container beside the application, a service on the same private network, a model
    /// server the operator deployed. The content leaves the process and stays inside the
    /// boundary the deployment's own risk assessment drew. A vendor's private endpoint is not
    /// this, unless that assessment says it is.
    /// </remarks>
    SelfHosted = 2,

    /// <summary>
    /// The component runs outside the boundary, as a third party's service.
    /// </summary>
    /// <remarks>
    /// Raw, un-sanitized content is transmitted to someone else in order to decide whether it
    /// may be transmitted to someone else. That is a defensible choice for an application to
    /// make, and it is not one the gateway makes on its behalf: it is never selected
    /// automatically, and the strict guarantee does not hold for the parts it handles.
    /// </remarks>
    Remote = 3,
}
