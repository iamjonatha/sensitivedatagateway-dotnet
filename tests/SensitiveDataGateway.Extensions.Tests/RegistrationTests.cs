using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using Xunit;

namespace SensitiveDataGateway.Extensions.Tests;

/// <summary>
/// What a container gets when it registers a gateway.
/// </summary>
public sealed class RegistrationTests
{
    [Fact]
    public void AddSensitiveDataGateway_PublishesTheGatewayAndThePolicyItWasConfiguredWith()
    {
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway());

        Assert.NotNull(provider.GetRequiredService<DocumentGateway>());
        Assert.NotNull(provider.GetRequiredService<ISensitiveDataGateway>());
        Assert.NotNull(provider.GetRequiredService<SanitizationPolicy>());
    }

    [Fact]
    public void AddSensitiveDataGateway_GovernsTheRegistrationWithTheStrictPolicyUntilToldOtherwise()
    {
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway());

        Assert.Same(SanitizationPolicy.Strict, provider.GetRequiredService<SanitizationPolicy>());
    }

    [Fact]
    public void AddSensitiveDataGateway_PublishesOneGateway()
    {
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway());

        var gateway = provider.GetRequiredService<DocumentGateway>();

        Assert.Same(gateway, provider.GetRequiredService<DocumentGateway>());
        Assert.Same(gateway, provider.GetRequiredService<ISensitiveDataGateway>());
    }

    [Fact]
    public void AddSensitiveDataGateway_PublishesThePolicyReadFromConfiguration()
    {
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway(
            builder => builder.WithPolicy(Harness.MinimalSection())));

        Assert.Equal("test.policy", provider.GetRequiredService<SanitizationPolicy>().Id.Value);
    }

    [Fact]
    public void AddSensitiveDataGateway_RefusesAPolicyItCannotBuildWhereItIsRegistered()
    {
        var services = new ServiceCollection();

        Assert.Throws<PolicyConfigurationException>(() => services.AddSensitiveDataGateway(
            builder => builder.WithPolicy(Harness.Section(("Version", "1")))));
    }

    [Fact]
    public void AddSensitiveDataGateway_RefusesASecondUnnamedRegistration()
    {
        var services = new ServiceCollection();
        services.AddSensitiveDataGateway();

        var failure = Assert.Throws<InvalidOperationException>(services.AddSensitiveDataGateway);

        Assert.Contains("unnamed", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSensitiveDataGateway_RefusesASecondRegistrationOfTheSameName()
    {
        var services = new ServiceCollection();
        services.AddSensitiveDataGateway("underwriting", static _ => { });

        var failure = Assert.Throws<InvalidOperationException>(
            () => services.AddSensitiveDataGateway("underwriting", static _ => { }));

        Assert.Contains("underwriting", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSensitiveDataGateway_KeepsNamedRegistrationsApartFromEachOtherAndFromTheUnnamedOne()
    {
        using var provider = Harness.Build(services =>
        {
            services.AddSensitiveDataGateway(builder => builder.WithPolicy(Harness.Policy("default")));
            services.AddSensitiveDataGateway(
                "underwriting",
                builder => builder.WithPolicy(Harness.Policy("underwriting")));
            services.AddSensitiveDataGateway("fraud", builder => builder.WithPolicy(Harness.Policy("fraud")));
        });

        Assert.Equal("default", provider.GetRequiredService<SanitizationPolicy>().Id.Value);
        Assert.Equal("underwriting", provider.GetRequiredKeyedService<SanitizationPolicy>("underwriting").Id.Value);
        Assert.Equal("fraud", provider.GetRequiredKeyedService<SanitizationPolicy>("fraud").Id.Value);
        Assert.NotSame(
            provider.GetRequiredKeyedService<DocumentGateway>("underwriting"),
            provider.GetRequiredKeyedService<DocumentGateway>("fraud"));
    }

    [Fact]
    public void AddSensitiveDataGateway_PublishesANamedGatewayUnderBothOfItsServiceTypes()
    {
        using var provider = Harness.Build(
            services => services.AddSensitiveDataGateway("fraud", static _ => { }));

        Assert.Same(
            provider.GetRequiredKeyedService<DocumentGateway>("fraud"),
            provider.GetRequiredKeyedService<ISensitiveDataGateway>("fraud"));
    }

    [Fact]
    public void AddSensitiveDataGateway_DoesNotPublishANamedGatewayAsAnUnnamedOne()
    {
        using var provider = Harness.Build(
            services => services.AddSensitiveDataGateway("fraud", static _ => { }));

        Assert.Null(provider.GetService<DocumentGateway>());
    }

    [Fact]
    public void AddSensitiveDataGateway_RefusesArgumentsItCannotUse()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddSensitiveDataGateway());
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(null!));
        Assert.Throws<ArgumentException>(() => services.AddSensitiveDataGateway("  ", static _ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway("fraud", null!));
    }

    [Fact]
    public async Task AddSensitiveDataGateway_ComposesAGatewayThatSanitizes()
    {
        using var provider = Harness.Build(
            services => services.AddSensitiveDataGateway(builder => builder.WithPolicy(TestPolicy.Permissive)));

        var gateway = provider.GetRequiredService<ISensitiveDataGateway>();
        var policy = provider.GetRequiredService<SanitizationPolicy>();
        var bundle = TestBundle.FromText("Contact GB71NWBK99999999999999 about this.");

        var result = await gateway.SanitizeAsync(bundle, policy, CancellationToken.None);

        Assert.True(result.IsAllowed);
        LeakAssert.NoneOf(result.Payload!, "GB71NWBK99999999999999");
    }

    [Fact]
    public async Task AddSensitiveDataGateway_SendsAuditRecordsToTheSinkTheRegistrationNamed()
    {
        var sink = new RecordingAuditSink();
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway(builder => builder
            .WithPolicy(TestPolicy.Permissive)
            .WithAuditSink(_ => sink)));

        await Sanitize(provider);

        Assert.Single(sink.Records);
    }

    [Fact]
    public async Task AddSensitiveDataGateway_SendsAuditRecordsToTheSinkTheContainerAlreadyHolds()
    {
        var sink = new RecordingAuditSink();
        using var provider = Harness.Build(services =>
        {
            services.AddSingleton<IAuditSink>(sink);
            services.AddSensitiveDataGateway(builder => builder.WithPolicy(TestPolicy.Permissive));
        });

        await Sanitize(provider);

        Assert.Single(sink.Records);
    }

    [Fact]
    public async Task AddSensitiveDataGateway_ReportsTheVersionTheRegistrationStated()
    {
        var sink = new RecordingAuditSink();
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway(builder => builder
            .WithPolicy(TestPolicy.Permissive)
            .WithGatewayVersion("9.9.9-test")
            .WithAuditSink(_ => sink)));

        await Sanitize(provider);

        Assert.Equal("9.9.9-test", sink.Records[0].GatewayVersion);
    }

    [Fact]
    public async Task AddSensitiveDataGateway_ReadsTheClockTheRegistrationStated()
    {
        var clock = new FixedClock(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var sink = new RecordingAuditSink();
        using var provider = Harness.Build(services => services.AddSensitiveDataGateway(builder => builder
            .WithPolicy(TestPolicy.Permissive)
            .WithTimeProvider(clock)
            .WithAuditSink(_ => sink)));

        await Sanitize(provider);

        Assert.Equal(clock.GetUtcNow(), sink.Records[0].StartedAt);
    }

    [Fact]
    public void Builder_RefusesArgumentsItCannotUse()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithPolicy((SanitizationPolicy)null!)));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithExtractor(null!)));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithDetector(null!)));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithAuditSink(null!)));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithPseudonymStore(null!)));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithHashingKey(null!)));
        Assert.Throws<ArgumentNullException>(() => services.AddSensitiveDataGateway(
            static builder => builder.WithTimeProvider(null!)));
    }

    [Fact]
    public void Builder_TellsTheRegistrationWhatItIsBeingAddedTo()
    {
        var services = new ServiceCollection();
        SensitiveDataGatewayBuilder? seen = null;

        services.AddSensitiveDataGateway("fraud", builder => seen = builder);

        Assert.Same(services, seen!.Services);
        Assert.Equal("fraud", seen.Name);
    }

    [Fact]
    public void Builder_CallsTheUnnamedRegistrationUnnamed()
    {
        var services = new ServiceCollection();
        SensitiveDataGatewayBuilder? seen = null;

        services.AddSensitiveDataGateway(builder => seen = builder);

        Assert.Null(seen!.Name);
    }

    private static async Task Sanitize(IServiceProvider provider)
    {
        var gateway = provider.GetRequiredService<ISensitiveDataGateway>();
        var policy = provider.GetRequiredService<SanitizationPolicy>();

        await gateway.SanitizeAsync(TestBundle.FromText("Nothing to see."), policy, CancellationToken.None);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
