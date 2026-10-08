namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Where pseudonym maps live for the lifetime of their scope.
/// </summary>
/// <remarks>
/// <para>
/// The store is the reason an anonymizer can allocate a token without handing anyone the way
/// back: the map goes here, and here is inside the trust boundary by construction. An
/// application that wants to re-identify asks the store; nothing else can.
/// </para>
/// <para>
/// The contract is synchronous on purpose. A store that had to be awaited would be a store
/// that reaches something, and nothing on the sanitization path is allowed to reach the
/// network. A persisting implementation writes locally, encrypts at rest, scopes its entries
/// and expires them — and none ships as a default, because the default lifetime of a map is
/// the operation that created it.
/// </para>
/// </remarks>
public interface IPseudonymStore
{
    /// <summary>
    /// Returns the map for a scope, creating an empty one the first time.
    /// </summary>
    /// <param name="key">What the map is scoped to.</param>
    /// <returns>The map.</returns>
    PseudonymMap GetOrCreate(PseudonymScopeKey key);

    /// <summary>
    /// Returns the map for a scope, without creating one.
    /// </summary>
    /// <param name="key">What the map is scoped to.</param>
    /// <param name="map">The map, when the store holds one.</param>
    /// <returns><see langword="true"/> when the store holds a map for the scope.</returns>
    bool TryGet(PseudonymScopeKey key, out PseudonymMap? map);

    /// <summary>
    /// Drops the map for a scope.
    /// </summary>
    /// <param name="key">What the map was scoped to.</param>
    /// <returns><see langword="true"/> when a map was dropped.</returns>
    /// <remarks>
    /// Releasing is how the default lifetime is enforced. An application that keeps a map
    /// past the operation that created it has extended the life of a re-identification key,
    /// and should be able to point at the line where it decided to.
    /// </remarks>
    bool Release(PseudonymScopeKey key);
}
