using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Auditing;

namespace SensitiveDataGateway;

/// <summary>
/// What a gateway does around the pipeline, rather than inside it.
/// </summary>
/// <remarks>
/// Everything the stages themselves need is configured on the stages. What is left here is
/// what the gateway owns: who hears about an operation, which clock it reads, and whether the
/// audit trail may correlate records back to content.
/// </remarks>
public sealed class GatewayOptions
{
    private static readonly string s_defaultVersion = ReadAssemblyVersion();

    private GatewayOptions(
        string gatewayVersion,
        IAuditSink auditSink,
        HashingKey? hashingKey,
        TimeProvider timeProvider,
        ILogger logger)
    {
        GatewayVersion = gatewayVersion;
        AuditSink = auditSink;
        HashingKey = hashingKey;
        TimeProvider = timeProvider;
        Logger = logger;
    }

    /// <summary>
    /// Gets the options a gateway uses when nothing was configured.
    /// </summary>
    /// <value>
    /// The assembly version, a sink that discards, the system clock, and no hashing key.
    /// Discarding is the default because an audit trail nobody reads is worth less than an
    /// application that had to choose where it goes.
    /// </value>
    public static GatewayOptions Default { get; } = new(
        s_defaultVersion,
        NullAuditSink.Instance,
        hashingKey: null,
        TimeProvider.System,
        NullLogger.Instance);

    /// <summary>
    /// Gets the version recorded in provenance and in every audit record.
    /// </summary>
    public string GatewayVersion { get; }

    /// <summary>
    /// Gets where audit records go.
    /// </summary>
    public IAuditSink AuditSink { get; }

    /// <summary>
    /// Gets the secret that keys every digest the gateway produces, when the application
    /// supplied one.
    /// </summary>
    /// <value>
    /// <see langword="null"/>, and then no audit record carries a digest, and the hashing
    /// transformations a policy asks for are keyed with a per-instance secret that no other
    /// process can reproduce. Correlating two operations over the same document is a
    /// capability, and a capability that is on by default is one nobody decided to have.
    /// </value>
    public HashingKey? HashingKey { get; }

    /// <summary>
    /// Gets the clock the gateway reads.
    /// </summary>
    /// <remarks>
    /// The processing budget is enforced against this clock too, so a test can exhaust a
    /// budget without waiting for it.
    /// </remarks>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets where the gateway writes what it is doing.
    /// </summary>
    /// <remarks>
    /// Nothing written here can carry document content, by construction: every log statement
    /// the gateway emits takes counts, identifiers and enumerations, and a test refuses a
    /// statement that takes a string.
    /// </remarks>
    public ILogger Logger { get; }

    /// <summary>
    /// Returns options that record a different version.
    /// </summary>
    /// <param name="gatewayVersion">The version to record.</param>
    /// <returns>New options. This instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gatewayVersion"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="gatewayVersion"/> is empty, blank, or longer than
    /// <see cref="SanitizationProvenance.MaxGatewayVersionLength"/>.
    /// </exception>
    public GatewayOptions WithGatewayVersion(string gatewayVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayVersion);

        if (gatewayVersion.Length > SanitizationProvenance.MaxGatewayVersionLength)
        {
            throw new ArgumentException(
                $"A gateway version may not exceed {SanitizationProvenance.MaxGatewayVersionLength} characters.",
                nameof(gatewayVersion));
        }

        return new GatewayOptions(gatewayVersion, AuditSink, HashingKey, TimeProvider, Logger);
    }

    /// <summary>
    /// Returns options that send audit records somewhere.
    /// </summary>
    /// <param name="auditSink">Where the records go.</param>
    /// <returns>New options. This instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="auditSink"/> is <see langword="null"/>.</exception>
    public GatewayOptions WithAuditSink(IAuditSink auditSink)
    {
        ArgumentNullException.ThrowIfNull(auditSink);

        return new GatewayOptions(GatewayVersion, auditSink, HashingKey, TimeProvider, Logger);
    }

    /// <summary>
    /// Returns options that key every digest the gateway produces.
    /// </summary>
    /// <param name="hashingKey">The secret that keys them.</param>
    /// <returns>New options. This instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hashingKey"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// One key covers two jobs. It keys the digests in an audit record, which let an operator
    /// ask "did we see this document before?" without keeping the document; whoever holds the
    /// key can ask that question, whoever does not cannot test a guess against a record. It
    /// also keys the hashing transformations a policy asks for, so the same value hashes to
    /// the same token across runs and processes rather than only within one.
    /// </remarks>
    public GatewayOptions WithHashing(HashingKey hashingKey)
    {
        ArgumentNullException.ThrowIfNull(hashingKey);

        return new GatewayOptions(GatewayVersion, AuditSink, hashingKey, TimeProvider, Logger);
    }

    /// <summary>
    /// Returns options that read a different clock.
    /// </summary>
    /// <param name="timeProvider">The clock to read.</param>
    /// <returns>New options. This instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public GatewayOptions WithTimeProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new GatewayOptions(GatewayVersion, AuditSink, HashingKey, timeProvider, Logger);
    }

    /// <summary>
    /// Returns options that log somewhere.
    /// </summary>
    /// <param name="logger">Where the gateway writes what it is doing.</param>
    /// <returns>New options. This instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public GatewayOptions WithLogger(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return new GatewayOptions(GatewayVersion, AuditSink, HashingKey, TimeProvider, logger);
    }

    /// <summary>
    /// Returns a description of the options.
    /// </summary>
    /// <returns>The version and whether hashing is on. Never the key.</returns>
    public override string ToString() =>
        $"Gateway {GatewayVersion}, hashing {(HashingKey is null ? "off" : "on")}";

    private static string ReadAssemblyVersion()
    {
        var informational = typeof(GatewayOptions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        // The informational version carries a "+<commit>" suffix that says nothing about behaviour.
        var separator = informational.IndexOf('+', StringComparison.Ordinal);
        var version = separator < 0 ? informational : informational[..separator];

        return version.Length > SanitizationProvenance.MaxGatewayVersionLength
            ? version[..SanitizationProvenance.MaxGatewayVersionLength]
            : version;
    }
}
