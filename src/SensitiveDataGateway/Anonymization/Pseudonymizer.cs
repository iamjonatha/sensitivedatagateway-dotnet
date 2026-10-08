using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Replaces a value with a stable token, and remembers the way back.
/// </summary>
/// <remarks>
/// A pseudonym is the only reversible transformation, and the reversal lives in the map this
/// was constructed with — which is inside the trust boundary, is not reachable from the
/// output, and is volatile by default. The same value of the same type always yields the same
/// token within one map, and means nothing in another.
/// </remarks>
public sealed class Pseudonymizer
{
    private readonly PseudonymMap _map;

    /// <summary>
    /// Creates a pseudonymizer.
    /// </summary>
    /// <param name="map">Where allocations are recorded.</param>
    /// <exception cref="ArgumentNullException"><paramref name="map"/> is <see langword="null"/>.</exception>
    public Pseudonymizer(PseudonymMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        _map = map;
    }

    /// <summary>
    /// Gets how far the tokens this allocates stay correlatable.
    /// </summary>
    public PseudonymScope Scope => _map.Scope;

    /// <summary>
    /// Returns the replacement for a pseudonymized value.
    /// </summary>
    /// <param name="type">What kind of thing is being replaced.</param>
    /// <param name="value">The value, which is document content.</param>
    /// <returns>The marker, for example <c>[PERSON_001]</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="type"/> is the default value, or <paramref name="value"/> is empty.
    /// </exception>
    public string Apply(EntityType type, string value) => _map.Allocate(type, value);
}
