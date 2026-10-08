using System;
using System.Collections.Generic;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The way back from a token to the value it replaced.
/// </summary>
/// <remarks>
/// <para>
/// This is the most dangerous artifact the gateway produces. A document is prose with
/// sensitive values buried in it; a map is the values alone, indexed and stripped of
/// everything that made them hard to find. It therefore stays where it was created, and
/// nothing downstream can name it: it is not a member of <see cref="SanitizedDocument"/>, not
/// a member of a payload, and not visible to a provider package, which cannot reference this
/// assembly.
/// </para>
/// <para>
/// Allocation is idempotent within a map: the same value of the same type always yields the
/// token it was first given, which is what makes a token correlatable within its scope and
/// meaningless outside it.
/// </para>
/// </remarks>
public sealed class PseudonymMap
{
    private readonly Dictionary<(EntityType Type, string Value), string> _tokens = new();
    private readonly Dictionary<string, string> _originals = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityType, int> _sequences = new();
    private readonly object _gate = new();

    private PseudonymMap(PseudonymScopeKey key) => Key = key;

    /// <summary>
    /// Gets what the map is scoped to.
    /// </summary>
    public PseudonymScopeKey Key { get; }

    /// <summary>
    /// Gets how far a token from this map stays correlatable.
    /// </summary>
    public PseudonymScope Scope => Key.Scope;

    /// <summary>
    /// Gets the number of distinct values the map holds.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _originals.Count;
            }
        }
    }

    /// <summary>
    /// Creates an empty map.
    /// </summary>
    /// <param name="key">What the map is scoped to.</param>
    /// <returns>The map.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the default value.</exception>
    public static PseudonymMap Create(PseudonymScopeKey key)
    {
        if (key.IsDefault)
        {
            throw new ArgumentException("The scope key carries no value.", nameof(key));
        }

        return new PseudonymMap(key);
    }

    /// <summary>
    /// Returns the token for a value, allocating one the first time the value is seen.
    /// </summary>
    /// <param name="type">What kind of thing the value is.</param>
    /// <param name="value">The value, which is document content.</param>
    /// <returns>The token, for example <c>[PERSON_001]</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="type"/> is the default value, or <paramref name="value"/> is empty.
    /// </exception>
    public string Allocate(EntityType type, string value)
    {
        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            throw new ArgumentException("A pseudonym cannot stand for an empty value.", nameof(value));
        }

        lock (_gate)
        {
            if (_tokens.TryGetValue((type, value), out var existing))
            {
                return existing;
            }

            var sequence = _sequences.TryGetValue(type, out var last) ? last + 1 : 1;
            var token = MarkerSyntax.Pseudonym(type, sequence);

            _sequences[type] = sequence;
            _tokens[(type, value)] = token;
            _originals[token] = value;

            return token;
        }
    }

    /// <summary>
    /// Returns the value a token stands for.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="value">The value the token replaced, when the map holds one.</param>
    /// <returns><see langword="true"/> when the map holds the token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This is re-identification. It is a local operation of the application that owns the
    /// map, and there is no channel through which anything outside the process can ask for
    /// it.
    /// </remarks>
    public bool TryReveal(string token, out string? value)
    {
        ArgumentNullException.ThrowIfNull(token);

        lock (_gate)
        {
            return _originals.TryGetValue(token, out value);
        }
    }

    /// <summary>
    /// Determines whether the map has already allocated a token for a value.
    /// </summary>
    /// <param name="type">What kind of thing the value is.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value already has a token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public bool Holds(EntityType type, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            return _tokens.ContainsKey((type, value));
        }
    }

    /// <summary>
    /// Forgets every allocation.
    /// </summary>
    /// <remarks>
    /// Clearing drops the references the map holds. It is not a secure erase: the strings
    /// came from managed memory the gateway does not own and may survive in the heap until
    /// collection. A map is a short-lived artifact, not a vault.
    /// </remarks>
    public void Clear()
    {
        lock (_gate)
        {
            _tokens.Clear();
            _originals.Clear();
            _sequences.Clear();
        }
    }

    /// <summary>
    /// Returns a description of the map.
    /// </summary>
    /// <returns>
    /// The scope and the number of entries. It never includes a token or a value: a token is
    /// a stable identifier for a person, and a value is the thing the gateway exists to keep
    /// out of logs.
    /// </returns>
    public override string ToString() => $"{Key} ({Count} entries)";
}
