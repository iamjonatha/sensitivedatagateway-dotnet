using System;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Auditing;

/// <summary>
/// Receives one record per operation.
/// </summary>
/// <remarks>
/// <para>
/// A sink is where an application sends the trail: a table, an append-only log, a SIEM. The
/// gateway calls it once per operation and does not look at what it returns.
/// </para>
/// <para>
/// A sink must not throw. The gateway treats a failed write as a failed operation, because an
/// operation nobody recorded is one nobody can answer for, and failing closed is the only
/// answer this library gives.
/// </para>
/// </remarks>
public interface IAuditSink
{
    /// <summary>
    /// Writes one record.
    /// </summary>
    /// <param name="record">The record. It carries no document content.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the record is durable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is <see langword="null"/>.</exception>
    ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}
