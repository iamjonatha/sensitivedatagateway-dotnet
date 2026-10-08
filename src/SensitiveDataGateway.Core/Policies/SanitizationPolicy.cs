using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// The rules a document is sanitized under.
/// </summary>
/// <remarks>
/// <para>
/// A policy is the only place in the library where a judgement is written down. Detectors
/// state what they found, the anonymizer applies what was decided, and everything between
/// those two facts is here, in a form that can be reviewed, versioned and committed to a
/// repository.
/// </para>
/// <para>
/// A policy is validated when it is built. An unusable policy is a configuration failure at
/// that point, never a permissive default discovered halfway through a real document.
/// </para>
/// <para>
/// Two policies ship with the library, <see cref="Strict"/> and <see cref="Balanced"/>. They
/// are starting points: an application that knows what it receives is expected to state its
/// own.
/// </para>
/// </remarks>
public sealed partial class SanitizationPolicy
{
    private readonly HashSet<EntityType> _mentioned;

    internal SanitizationPolicy(SanitizationPolicyBuilder builder)
    {
        Id = builder.Id;
        Version = builder.Version;
        DefaultAction = builder.DefaultAction;
        UnknownEntityAction = builder.UnknownEntityAction;
        DefaultThresholds = builder.DefaultThresholds;
        DefaultScope = builder.DefaultScope;
        Limits = builder.Limits;
        UnresolvedTolerance = builder.UnresolvedTolerance;
        RequiresCompleteScan = builder.RequiresCompleteScan;
        Rules = new ReadOnlyCollection<PolicyRule>(new List<PolicyRule>(builder.Rules));
        Thresholds = new ReadOnlyDictionary<EntityType, ConfidenceThresholds>(
            new Dictionary<EntityType, ConfidenceThresholds>(builder.Thresholds));

        _mentioned = new HashSet<EntityType>(Thresholds.Keys);

        foreach (var rule in Rules)
        {
            _mentioned.Add(rule.Match);
        }
    }

    /// <summary>
    /// Gets the identifier of the policy.
    /// </summary>
    public PolicyId Id { get; }

    /// <summary>
    /// Gets the revision of the policy.
    /// </summary>
    public PolicyVersion Version { get; }

    /// <summary>
    /// Gets what happens to a candidate whose type the policy mentions but whose rules do not
    /// match.
    /// </summary>
    public TransformationAction DefaultAction { get; }

    /// <summary>
    /// Gets what happens to a candidate whose type the policy does not mention at all.
    /// </summary>
    /// <remarks>
    /// The shipped value is <see cref="TransformationAction.Block"/>, and that is not an
    /// arbitrary choice of default. An unrecognized category is an unknown, and this library
    /// does not assume an unknown is harmless.
    /// </remarks>
    public TransformationAction UnknownEntityAction { get; }

    /// <summary>
    /// Gets the thresholds applied to an entity type the policy states no others for.
    /// </summary>
    public ConfidenceThresholds DefaultThresholds { get; }

    /// <summary>
    /// Gets how far a replacement stays correlatable when a rule does not state a scope.
    /// </summary>
    public PseudonymScope DefaultScope { get; }

    /// <summary>
    /// Gets the rules, in the order they are evaluated.
    /// </summary>
    /// <remarks>
    /// The first rule that matches wins. The policy refuses to hold a rule that an earlier
    /// one would make unreachable, so the order is a statement of specificity rather than a
    /// trap.
    /// </remarks>
    public IReadOnlyList<PolicyRule> Rules { get; }

    /// <summary>
    /// Gets the thresholds stated per entity type.
    /// </summary>
    public IReadOnlyDictionary<EntityType, ConfidenceThresholds> Thresholds { get; }

    /// <summary>
    /// Gets the size, count and time bounds an operation under this policy must stay inside.
    /// </summary>
    public ProcessingLimits Limits { get; }

    /// <summary>
    /// Gets the highest sensitivity an unresolved candidate may carry without blocking.
    /// </summary>
    /// <value>
    /// The tolerated level, or <see langword="null"/> when no unresolved candidate is
    /// tolerated.
    /// </value>
    /// <remarks>
    /// An unresolved candidate keeps its text, so tolerating one is a decision to transmit
    /// it. Stating the line here makes that decision visible in the policy instead of
    /// implicit in a threshold.
    /// </remarks>
    public SensitivityLevel? UnresolvedTolerance { get; }

    /// <summary>
    /// Gets a value indicating whether every part of the bundle must have been read and
    /// scanned.
    /// </summary>
    /// <remarks>
    /// When this is set, a failed extractor or a failed detector blocks the operation. A
    /// document that was not fully examined has not been shown to be safe.
    /// </remarks>
    public bool RequiresCompleteScan { get; }

    /// <summary>
    /// Starts building a policy.
    /// </summary>
    /// <param name="id">The identifier of the policy.</param>
    /// <param name="version">The revision of the policy.</param>
    /// <returns>
    /// A builder whose every default is the closed one: block what is not understood, resolve
    /// nothing on weak evidence, tolerate no unresolved candidate and require a complete
    /// scan.
    /// </returns>
    /// <exception cref="ArgumentException">An identifier is the default value.</exception>
    public static SanitizationPolicyBuilder CreateBuilder(PolicyId id, PolicyVersion version) =>
        new(id, version);

    /// <summary>
    /// Determines whether the policy says anything at all about an entity type.
    /// </summary>
    /// <param name="type">The entity type.</param>
    /// <returns>
    /// <see langword="true"/> when a rule governs the type or the policy states thresholds
    /// for it.
    /// </returns>
    /// <remarks>
    /// A type the policy does not mention is governed by <see cref="UnknownEntityAction"/>.
    /// </remarks>
    public bool Mentions(EntityType type) => _mentioned.Contains(type);

    /// <summary>
    /// Returns the thresholds that apply to an entity type.
    /// </summary>
    /// <param name="type">The entity type.</param>
    /// <returns>
    /// The thresholds stated for the type, or <see cref="DefaultThresholds"/> when the policy
    /// states none.
    /// </returns>
    public ConfidenceThresholds ThresholdsFor(EntityType type) =>
        Thresholds.TryGetValue(type, out var thresholds) ? thresholds : DefaultThresholds;

    /// <summary>
    /// Finds the first rule that governs a candidate.
    /// </summary>
    /// <param name="entity">The candidate.</param>
    /// <param name="ruleIndex">
    /// When this method returns <see langword="true"/>, the position of the rule in
    /// <see cref="Rules"/>; otherwise -1.
    /// </param>
    /// <returns><see langword="true"/> when a rule governs the candidate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    public bool TryFindRule(SensitiveEntity entity, out int ruleIndex)
    {
        ArgumentNullException.ThrowIfNull(entity);

        for (var index = 0; index < Rules.Count; index++)
        {
            if (Rules[index].Matches(entity))
            {
                ruleIndex = index;
                return true;
            }
        }

        ruleIndex = -1;
        return false;
    }

    /// <summary>
    /// Determines whether the policy accepts transmitting an unresolved candidate of a given
    /// sensitivity.
    /// </summary>
    /// <param name="sensitivity">The sensitivity the candidate carries.</param>
    /// <returns>
    /// <see langword="true"/> when <see cref="UnresolvedTolerance"/> is stated and is at
    /// least as high as <paramref name="sensitivity"/>.
    /// </returns>
    public bool ToleratesUnresolved(SensitivityLevel sensitivity) =>
        UnresolvedTolerance is { } tolerance && sensitivity <= tolerance;

    /// <summary>
    /// Describes the policy in the form configuration binds to.
    /// </summary>
    /// <returns>
    /// A description that produces an equivalent policy through
    /// <see cref="SanitizationPolicyDescription.ToPolicy"/>.
    /// </returns>
    public SanitizationPolicyDescription ToDescription()
    {
        var description = new SanitizationPolicyDescription
        {
            Id = Id.Value,
            Version = Version.Value,
            DefaultAction = DefaultAction,
            UnknownEntityAction = UnknownEntityAction,
            DefaultScope = DefaultScope,
            DefaultThresholds = ConfidenceThresholdsDescription.FromThresholds(DefaultThresholds),
            UnresolvedTolerance = UnresolvedTolerance?.ToString() ?? SanitizationPolicyDescription.NoTolerance,
            RequireCompleteScan = RequiresCompleteScan,
            Limits = ProcessingLimitsDescription.FromLimits(Limits),
        };

        foreach (var rule in Rules)
        {
            description.Rules.Add(PolicyRuleDescription.FromRule(rule));
        }

        foreach (var entry in Thresholds)
        {
            description.Thresholds.Add(entry.Key.Value, ConfidenceThresholdsDescription.FromThresholds(entry.Value));
        }

        return description;
    }

    /// <summary>
    /// Returns the identity of the policy.
    /// </summary>
    /// <returns>The identifier and the version. They carry no document content.</returns>
    public override string ToString() => $"{Id} {Version}";
}
