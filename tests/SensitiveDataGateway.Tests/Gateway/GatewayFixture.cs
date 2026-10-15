using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Tests.Gateway;

/// <summary>
/// Keeps every audit record the gateway wrote.
/// </summary>
internal sealed class RecordingAuditSink : IAuditSink
{
    private readonly List<AuditRecord> _records = [];

    public IReadOnlyList<AuditRecord> Records => _records;

    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        _records.Add(record);

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A sink that refuses to write, so a test can prove the gateway does not swallow it.
/// </summary>
internal sealed class FailingAuditSink : IAuditSink
{
    public ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The audit store is unavailable.");
}

/// <summary>
/// Keeps everything the gateway logged, rendered exactly as a sink would render it.
/// </summary>
/// <remarks>
/// It captures the formatted message and every state value separately, because a leak can
/// hide in a structured field that a console formatter never prints.
/// </remarks>
internal sealed class CapturingLogger : ILogger
{
    private readonly List<string> _written = [];

    public IReadOnlyList<string> Written => _written;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        _written.Add(state.ToString() ?? string.Empty);

        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _written.Add(formatter(state, exception));

        if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            foreach (var value in values)
            {
                _written.Add(value.Value?.ToString() ?? string.Empty);
            }
        }

        if (exception is not null)
        {
            _written.Add(exception.ToString());
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>
/// A provider that records whether it was reached.
/// </summary>
internal sealed class RecordingProvider : IDecisionProvider<string, string>
{
    public string Name => "test.recording";

    public int Calls { get; private set; }

    public ValidatedPayload? LastPayload { get; private set; }

    public Task<string> DecideAsync(ValidatedPayload payload, string request, CancellationToken cancellationToken)
    {
        Calls++;
        LastPayload = payload;

        return Task.FromResult($"{request}:{payload.Content.Count}");
    }
}

/// <summary>
/// A provider that fails the way a third-party library fails: with its own exception type,
/// not the one the interface documents.
/// </summary>
/// <remarks>
/// A provider lives outside the trust boundary and cannot be made to honour a contract. The
/// gateway has to report the failure as its own anyway, otherwise the audit record of a
/// provider outage says only that something went wrong.
/// </remarks>
internal sealed class ThrowingProvider : IDecisionProvider<string, string>
{
    public string Name => "test.throwing";

    public Task<string> DecideAsync(ValidatedPayload payload, string request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The decision service is unavailable.");
}

/// <summary>
/// A provider that observes cancellation instead of deciding.
/// </summary>
internal sealed class CancellingProvider : IDecisionProvider<string, string>
{
    public string Name => "test.cancelling";

    public Task<string> DecideAsync(ValidatedPayload payload, string request, CancellationToken cancellationToken) =>
        throw new OperationCanceledException();
}

/// <summary>
/// A provider that fails with whatever it is handed.
/// </summary>
internal sealed class FakeProvider(Exception failure) : IDecisionProvider<string, string>
{
    public static FakeProvider Failing(Exception failure) => new(failure);

    public string Name => "test.fake";

    public Task<string> DecideAsync(ValidatedPayload payload, string request, CancellationToken cancellationToken) =>
        throw failure;
}

/// <summary>
/// A provider that forgot to name itself.
/// </summary>
internal sealed class UnnamedProvider : IDecisionProvider<string, string>
{
    public string Name => "   ";

    public Task<string> DecideAsync(ValidatedPayload payload, string request, CancellationToken cancellationToken) =>
        Task.FromResult("decided");
}

/// <summary>
/// A provider whose name is different every time it is read.
/// </summary>
/// <remarks>
/// It stands in for the provider that would build its name out of whatever it was last asked
/// to decide on. The gateway reads the name once, at composition, so a name derived from a
/// document has nothing to derive from and cannot reach the audit trail.
/// </remarks>
internal sealed class ShiftingNameProvider : IDecisionProvider<string, string>
{
    public int Reads { get; private set; }

    public string Name => string.Create(CultureInfo.InvariantCulture, $"test.shifting.{++Reads}");

    public Task<string> DecideAsync(ValidatedPayload payload, string request, CancellationToken cancellationToken) =>
        Task.FromResult("decided");
}

/// <summary>
/// Builds the gateways and bundles the gateway tests use.
/// </summary>
/// <remarks>
/// Every value in here is invented. None of it is a real name, address or account.
/// </remarks>
internal static class GatewayFixture
{
    public static DocumentBundle Bundle(string text) => Bundle("bundle", text);

    public static DocumentBundle Bundle(string id, string text) =>
        DocumentBundle.Create(
            BundleId.Create(id),
            DocumentPart.Create(
                PartId.Create("body"),
                PartKind.Body,
                "text/plain",
                DocumentContent.FromText(text)));

    public static DocumentBundle Unreadable() =>
        DocumentBundle.Create(
            BundleId.Create("bundle"),
            DocumentPart.Create(
                PartId.Create("body"),
                PartKind.Body,
                "application/pdf",
                DocumentContent.FromBytes(new byte[] { 1, 2, 3 })));

    public static GatewayOptions Options(RecordingAuditSink sink) =>
        GatewayOptions.Default.WithAuditSink(sink);

    public static DocumentGateway Gateway(GatewayOptions options) => DocumentGateway.CreateDefault(options);

    public static DocumentGateway Gateway(RecordingAuditSink sink) => Gateway(Options(sink));

    public static SanitizationPolicy TightLimits(int maxEntities) =>
        SanitizationPolicy.CreateBuilder(PolicyId.Create("test.tight"), PolicyVersion.Create("1.0.0"))
            .WithDefaultAction(Anonymization.TransformationAction.Pseudonymize)
            .WithLimits(ProcessingLimits.Create(
                maxBundleBytes: 65_536,
                maxPartBytes: 65_536,
                maxPartCount: 8,
                maxExtractedCharacters: 65_536,
                maxEntities,
                maxProcessingTime: TimeSpan.FromMinutes(1)))
            .Build();
}
