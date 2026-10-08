using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The default store: maps live in memory, and die with the process.
/// </summary>
/// <remarks>
/// Volatile is the default because the alternative is a durable re-identification key, and a
/// durable re-identification key should be something an application asked for in writing.
/// Nothing here is written to disk, encrypted or otherwise, and nothing survives a restart.
/// </remarks>
public sealed class InMemoryPseudonymStore : IPseudonymStore
{
    private readonly Dictionary<PseudonymScopeKey, PseudonymMap> _maps = new();
    private readonly object _gate = new();

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the default value.</exception>
    public PseudonymMap GetOrCreate(PseudonymScopeKey key)
    {
        RequireKey(key);

        lock (_gate)
        {
            if (_maps.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var created = PseudonymMap.Create(key);
            _maps[key] = created;

            return created;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the default value.</exception>
    public bool TryGet(PseudonymScopeKey key, out PseudonymMap? map)
    {
        RequireKey(key);

        lock (_gate)
        {
            return _maps.TryGetValue(key, out map);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the default value.</exception>
    public bool Release(PseudonymScopeKey key)
    {
        RequireKey(key);

        lock (_gate)
        {
            if (!_maps.Remove(key, out var released))
            {
                return false;
            }

            released.Clear();

            return true;
        }
    }

    /// <summary>
    /// Drops every map the store holds.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            foreach (var map in _maps.Values)
            {
                map.Clear();
            }

            _maps.Clear();
        }
    }

    private static void RequireKey(PseudonymScopeKey key)
    {
        if (key.IsDefault)
        {
            throw new ArgumentException("The scope key carries no value.", nameof(key));
        }
    }
}
