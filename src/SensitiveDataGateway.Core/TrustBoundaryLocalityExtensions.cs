namespace SensitiveDataGateway;

/// <summary>
/// The questions the library asks about a <see cref="TrustBoundaryLocality"/>.
/// </summary>
/// <remarks>
/// Two stages classify a component by where it runs, and both need the same two answers: is
/// it inside, and how is it written down. Answering them in one place keeps a recognizer and
/// a detector from drifting into disagreeing about what "self-hosted" means.
/// </remarks>
public static class TrustBoundaryLocalityExtensions
{
    /// <summary>
    /// Determines whether a component at this locality reads content inside the boundary.
    /// </summary>
    /// <param name="locality">Where the component runs.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="TrustBoundaryLocality.InProcess"/> and
    /// <see cref="TrustBoundaryLocality.SelfHosted"/>. An undeclared value is outside,
    /// because a component that has not said where it runs has not been reviewed.
    /// </returns>
    public static bool IsInsideTheTrustBoundary(this TrustBoundaryLocality locality) =>
        locality is TrustBoundaryLocality.InProcess or TrustBoundaryLocality.SelfHosted;

    /// <summary>
    /// Determines whether a value is one of the declared localities.
    /// </summary>
    /// <param name="locality">The value to check.</param>
    /// <returns><see langword="true"/> when the value is declared.</returns>
    public static bool IsDeclared(this TrustBoundaryLocality locality) =>
        locality is TrustBoundaryLocality.InProcess
            or TrustBoundaryLocality.SelfHosted
            or TrustBoundaryLocality.Remote;

    /// <summary>
    /// Writes a locality the way an audit record spells it.
    /// </summary>
    /// <param name="locality">Where the component runs.</param>
    /// <returns>The locality in words, or <c>undeclared</c> for an undeclared value.</returns>
    public static string Describe(this TrustBoundaryLocality locality) => locality switch
    {
        TrustBoundaryLocality.InProcess => "in-process",
        TrustBoundaryLocality.SelfHosted => "self-hosted",
        TrustBoundaryLocality.Remote => "remote",
        _ => "undeclared",
    };
}
