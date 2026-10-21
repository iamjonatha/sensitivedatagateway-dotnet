namespace SensitiveDataGateway.Core.Tests;

/// <summary>
/// The locality is the one property the library reasons about when a component reads raw
/// content, so the line it draws is asserted rather than assumed.
/// </summary>
public sealed class TrustBoundaryLocalityTests
{
    [Theory]
    [InlineData(TrustBoundaryLocality.InProcess, true)]
    [InlineData(TrustBoundaryLocality.SelfHosted, true)]
    [InlineData(TrustBoundaryLocality.Remote, false)]
    public void Puts_the_boundary_between_self_hosted_and_remote(TrustBoundaryLocality locality, bool inside) =>
        Assert.Equal(inside, locality.IsInsideTheTrustBoundary());

    [Fact]
    public void Treats_a_locality_nobody_declared_as_outside() =>
        Assert.False(((TrustBoundaryLocality)0).IsInsideTheTrustBoundary());

    [Theory]
    [InlineData(TrustBoundaryLocality.InProcess)]
    [InlineData(TrustBoundaryLocality.SelfHosted)]
    [InlineData(TrustBoundaryLocality.Remote)]
    public void Recognises_every_declared_locality(TrustBoundaryLocality locality) =>
        Assert.True(locality.IsDeclared());

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Refuses_a_value_that_names_no_locality(int value) =>
        Assert.False(((TrustBoundaryLocality)value).IsDeclared());

    [Theory]
    [InlineData(TrustBoundaryLocality.InProcess, "in-process")]
    [InlineData(TrustBoundaryLocality.SelfHosted, "self-hosted")]
    [InlineData(TrustBoundaryLocality.Remote, "remote")]
    public void Spells_out_where_a_component_runs(TrustBoundaryLocality locality, string expected) =>
        Assert.Equal(expected, locality.Describe());

    [Fact]
    public void Says_undeclared_rather_than_guessing() =>
        Assert.Equal("undeclared", ((TrustBoundaryLocality)0).Describe());
}
