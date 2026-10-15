using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Extensions;

/// <summary>
/// Describes one gateway registration while it is being composed.
/// </summary>
/// <remarks>
/// <para>
/// Everything a gateway is made of is stated here as a value or as a factory over the service
/// provider. There is no overload that takes a type and constructs it, because constructing a
/// type the compiler was never shown is the one thing that would stop this package working
/// under trimming and native ahead-of-time compilation.
/// </para>
/// <para>
/// The builder does not register the policy as something the gateway will reach for later. A
/// policy stays an argument of every call, so the registration publishes it and the caller
/// passes it, deliberately, to the operation it governs.
/// </para>
/// </remarks>
public sealed class SensitiveDataGatewayBuilder
{
    private readonly List<IDocumentExtractor> _extractors = [];
    private readonly List<ISensitiveDataDetector> _detectors = [];
    private Func<IServiceProvider, IAuditSink>? _auditSink;
    private Func<IServiceProvider, IPseudonymStore>? _pseudonymStore;
    private HashingKey? _hashingKey;
    private TimeProvider? _timeProvider;
    private string? _gatewayVersion;

    internal SensitiveDataGatewayBuilder(IServiceCollection services, string? name)
    {
        Services = services;
        Name = name;
    }

    /// <summary>
    /// Gets the collection this registration is being added to.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets the name of this registration, or <see langword="null"/> for the unnamed one.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the policy this registration publishes.
    /// </summary>
    /// <value>
    /// <see cref="SanitizationPolicy.Strict"/> until something says otherwise, so a
    /// registration that forgets to state a policy blocks rather than leaks.
    /// </value>
    public SanitizationPolicy Policy { get; private set; } = SanitizationPolicy.Strict;

    /// <summary>
    /// States the policy this registration publishes.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is <see langword="null"/>.</exception>
    public SensitiveDataGatewayBuilder WithPolicy(SanitizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Policy = policy;

        return this;
    }

    /// <summary>
    /// Reads the policy this registration publishes out of configuration.
    /// </summary>
    /// <param name="configuration">The section the policy is written in.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="PolicyConfigurationException">
    /// The section does not denote a usable policy.
    /// </exception>
    /// <remarks>
    /// The policy is built here, while the application is still registering services, and not
    /// the first time somebody asks for it. A policy file that no longer parses stops a
    /// deployment at startup, where an operator is watching.
    /// </remarks>
    public SensitiveDataGatewayBuilder WithPolicy(IConfiguration configuration) =>
        WithPolicy(PolicyConfiguration.ReadPolicy(configuration));

    /// <summary>
    /// Adds an extractor. Extractors are tried in the order they are added.
    /// </summary>
    /// <param name="extractor">The extractor.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extractor"/> is <see langword="null"/>.</exception>
    public SensitiveDataGatewayBuilder WithExtractor(IDocumentExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        _extractors.Add(extractor);

        return this;
    }

    /// <summary>
    /// Adds a detector. Stating any detector replaces the deterministic set wholesale.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="detector"/> is <see langword="null"/>.</exception>
    public SensitiveDataGatewayBuilder WithDetector(ISensitiveDataDetector detector)
    {
        ArgumentNullException.ThrowIfNull(detector);
        _detectors.Add(detector);

        return this;
    }

    /// <summary>
    /// States the key that keyed hashing uses.
    /// </summary>
    /// <param name="hashingKey">The key.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hashingKey"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Without one, each gateway hashes under a key that dies with the process, so two
    /// instances of the same application disagree about every hash they produce.
    /// </remarks>
    public SensitiveDataGatewayBuilder WithHashingKey(HashingKey hashingKey)
    {
        ArgumentNullException.ThrowIfNull(hashingKey);
        _hashingKey = hashingKey;

        return this;
    }

    /// <summary>
    /// States the version the audit record reports for the gateway.
    /// </summary>
    /// <param name="gatewayVersion">The version.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="gatewayVersion"/> is empty or blank.</exception>
    public SensitiveDataGatewayBuilder WithGatewayVersion(string gatewayVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayVersion);
        _gatewayVersion = gatewayVersion;

        return this;
    }

    /// <summary>
    /// States the clock the gateway reads.
    /// </summary>
    /// <param name="timeProvider">The clock.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    public SensitiveDataGatewayBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;

        return this;
    }

    /// <summary>
    /// States where audit records go.
    /// </summary>
    /// <param name="auditSink">Resolves the sink out of the service provider.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="auditSink"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Without one, the registration uses the <see cref="IAuditSink"/> registered in the
    /// container, if there is one, and discards records otherwise.
    /// </remarks>
    public SensitiveDataGatewayBuilder WithAuditSink(Func<IServiceProvider, IAuditSink> auditSink)
    {
        ArgumentNullException.ThrowIfNull(auditSink);
        _auditSink = auditSink;

        return this;
    }

    /// <summary>
    /// States where pseudonyms live.
    /// </summary>
    /// <param name="pseudonymStore">Resolves the store out of the service provider.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pseudonymStore"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Without one, the registration keeps pseudonyms in the memory of the process. Two
    /// instances of the same application then disagree about every pseudonym, which is a
    /// correctness problem rather than a privacy one, but a visible one.
    /// </remarks>
    public SensitiveDataGatewayBuilder WithPseudonymStore(Func<IServiceProvider, IPseudonymStore> pseudonymStore)
    {
        ArgumentNullException.ThrowIfNull(pseudonymStore);
        _pseudonymStore = pseudonymStore;

        return this;
    }

    internal DocumentGateway Build(IServiceProvider services) =>
        DocumentGateway.Create(
            _extractors.Count == 0 ? [new PlainTextExtractor()] : _extractors,
            _detectors.Count == 0 ? DeterministicDetectors.All() : _detectors,
            _pseudonymStore is null ? new InMemoryPseudonymStore() : _pseudonymStore(services),
            OptionsFor(services));

    private GatewayOptions OptionsFor(IServiceProvider services)
    {
        var options = GatewayOptions.Default;

        if (_gatewayVersion is { } version)
        {
            options = options.WithGatewayVersion(version);
        }

        if (_hashingKey is { } hashingKey)
        {
            options = options.WithHashing(hashingKey);
        }

        if (_timeProvider is { } timeProvider)
        {
            options = options.WithTimeProvider(timeProvider);
        }

        if (AuditSinkFor(services) is { } auditSink)
        {
            options = options.WithAuditSink(auditSink);
        }

        return services.GetService<ILoggerFactory>() is { } factory
            ? options.WithLogger(factory.CreateLogger<DocumentGateway>())
            : options;
    }

    private IAuditSink? AuditSinkFor(IServiceProvider services) =>
        _auditSink is null ? services.GetService<IAuditSink>() : _auditSink(services);
}
