using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Auditing;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// An audit sink that keeps every record in memory.
/// </summary>
/// <remarks>
/// An audit record is content-free by construction, so holding one in memory is safe. Use
/// this to assert that an operation was audited, and to assert that the record describes
/// the operation without describing the document.
/// </remarks>
public sealed class RecordingAuditSink : IAuditSink
{
    private readonly List<AuditRecord> _records = [];

    /// <summary>
    /// Gets a snapshot of the records written so far, in the order they were written.
    /// </summary>
    public IReadOnlyList<AuditRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_records)
        {
            _records.Add(record);
        }

        return ValueTask.CompletedTask;
    }
}
