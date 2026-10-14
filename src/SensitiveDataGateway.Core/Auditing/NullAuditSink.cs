using System;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Auditing;

/// <summary>
/// The sink that keeps nothing.
/// </summary>
/// <remarks>
/// This is the default, so that a gateway works before an application has decided where its
/// trail belongs. It is not a recommendation: an installation that handles personal data and
/// keeps no record of what it sent is one that cannot answer a subject access request.
/// </remarks>
public sealed class NullAuditSink : IAuditSink
{
    /// <summary>
    /// Gets the single instance.
    /// </summary>
    public static NullAuditSink Instance { get; } = new();

    private NullAuditSink()
    {
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        return ValueTask.CompletedTask;
    }
}
