# Policies

A policy decides what happens to each thing a detector found. It is the only place in the
gateway where that choice is made, and it is deliberately the only stage that is meant to be
edited by someone who is not rebuilding the application.

This guide covers writing one, the two policies the library ships, and what each failure
means.

## The shape of a decision

The engine takes a `DetectedDocument` and a `SanitizationPolicy` and returns a
`TransformationPlan`. It reads candidates and produces decisions. It never touches text —
that is stage 4's job, described in [the pipeline](../architecture/pipeline.md).

```csharp
var engine = new PolicyEngine();
var plan = engine.Evaluate(detected, SanitizationPolicy.Strict);

if (plan.Blocking.Count > 0)
{
    // The document does not leave. Each violation says why, without quoting the value.
    return;
}
```

For each candidate, in this order:

1. The candidate's confidence is classified against the thresholds for its type — the ones
   stated for that type, or `DefaultThresholds`. Below the flag threshold the candidate is
   **unresolved**: it gets no decision at all, because the engine will not act on something
   it could not identify.
2. The first rule whose `Match` is the candidate's type, and whose `Sensitivity` is either
   absent or equal to the candidate's, wins.
3. If the policy mentions the type but no rule matched, `DefaultAction` applies.
4. If the policy does not mention the type at all — no rule, no thresholds entry —
   `UnknownEntityAction` applies. It ships as `Block`.

Every decision carries a `PolicyRationale` naming the rule or the default that produced it.
A decision without a reason is not auditable, so the type does not allow one.

## Writing a policy in code

```csharp
var policy = SanitizationPolicy.CreateBuilder(
        PolicyId.Create("support-tickets"),
        PolicyVersion.Create("3"))
    .WithDefaultAction(TransformationAction.Redact)
    .WithUnknownEntityAction(TransformationAction.Block)
    .WithDefaultThresholds(ConfidenceThresholds.Create(transform: 0.90, flag: 0.70))
    .WithThresholds(EntityType.ApiKey, ConfidenceThresholds.Create(0.50, 0.30))
    .WithDefaultScope(PseudonymScope.Bundle)
    .AddRule(PolicyRule.For(EntityType.ApiKey, TransformationAction.Block))
    .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Pseudonymize)
        .WithScope(PseudonymScope.Bundle))
    .TolerateUnresolvedUpTo(SensitivityLevel.Low)
    .Build();
```

The builder starts closed: block everything, tolerate nothing. A policy you configured
halfway is a restrictive policy, never an open one.

### Rule order matters, and the builder enforces it

The first matching rule wins. A rule that an earlier rule has already made unreachable is
not a harmless duplicate — it is a line a reviewer will read and believe. `Build()` rejects
it with `policy.unreachable_rule`.

```csharp
.AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Pseudonymize))
.AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact)
    .WhenSensitivityIs(SensitivityLevel.High))                 // rejected: unreachable
```

Put the narrower rule first.

### Sensitivity selects, it does not assign

`PolicyRule.Sensitivity` restricts the rule to candidates the detector classified at that
level. It never changes a candidate's sensitivity: that comes from detection, and a policy
cannot talk its way out of it.

### Scope applies to two actions

`PseudonymScope` is accepted only for `Pseudonymize` and `Hash`, the two actions that produce
a correlatable token. Stating it for `Redact` is rejected with
`policy.rule_scope_without_correlation`, because the scope would be a value nothing reads.

| Scope | A token is the same for the same value |
| --- | --- |
| `Document` | within one document |
| `Bundle` | across every part of one bundle |
| `Session` | across every operation in one session |
| `Tenant` | across every session of one tenant |

## Writing a policy in configuration

`SanitizationPolicyDescription` is what binds to `appsettings.json`, and `ToPolicy()` is the
single place that turns it into a policy. Binding is ordinary property binding — the library
never reflects over policy types to discover behaviour — so what the description can express
is exactly what the file below shows.

```json
{
  "Policy": {
    "Id": "support-tickets",
    "Version": "3",
    "DefaultAction": "Redact",
    "UnknownEntityAction": "Block",
    "DefaultScope": "Bundle",
    "DefaultThresholds": { "Transform": 0.90, "Flag": 0.70 },
    "UnresolvedTolerance": "Low",
    "Thresholds": {
      "API_KEY": { "Transform": 0.50, "Flag": 0.30 }
    },
    "Rules": [
      { "Match": "API_KEY", "Action": "Block" },
      { "Match": "PERSON", "Action": "Pseudonymize", "Scope": "Bundle" },
      { "Match": "EMAIL", "Action": "Mask" }
    ],
    "Limits": {
      "MaxBundleBytes": 33554432,
      "MaxPartBytes": 8388608,
      "MaxPartCount": 50,
      "MaxExtractedCharacters": 2000000,
      "MaxEntities": 5000,
      "MaxProcessingTime": "00:00:30"
    }
  }
}
```

```csharp
var description = configuration.GetSection("Policy").Get<SanitizationPolicyDescription>();
var policy = description.ToPolicy();
```

Omitting a value never loosens the policy. An absent `DefaultAction` is `Block` and an absent
`UnresolvedTolerance` tolerates nothing. Use `"None"` to state no tolerance explicitly.

Call `ToPolicy()` at startup, not per request. A malformed policy should stop the application
from starting, not fail on the first document that happens to contain a tax code.

## The shipped policies

Both cover every well-known entity type, because a type neither rule nor threshold mentions
falls to `UnknownEntityAction` and blocks.

| | `SanitizationPolicy.Strict` | `SanitizationPolicy.Balanced` |
| --- | --- | --- |
| Identifier | `strict` / `1.0` | `balanced` / `1.0` |
| Default action | `Redact` | `Pseudonymize` |
| Unknown type | `Block` | `Block` |
| Thresholds | 0.90 / 0.70 | 0.80 / 0.60 |
| Credentials and security findings | 0.50 / 0.30 | 0.50 / 0.30 |
| Unresolved tolerated up to | `Low` | `Moderate` |
| Complete scan required | yes | no |
| Credentials | `Block` | `Block` |
| Identity documents | `Redact` | `Hash` |
| Financial identifiers | `Redact` | `Mask` |
| Names, customers, internal references | `Pseudonymize` | `Pseudonymize` |
| Contact details | `Redact` | `Pseudonymize` |
| Special categories, date of birth | `Redact` | `Redact` |

`Strict` is the one to start from. It is what the gateway means by refusing to be useful
rather than refusing to be safe: anything it is unsure about either loses its content or
stops the operation.

`Balanced` keeps more of a document legible for an engine that needs to correlate — the same
person is the same token, an IBAN keeps its shape — at the cost of a wider surface than
`Strict`. It still blocks every credential, and it still blocks an entity type it has never
heard of.

Neither is a compliance control on its own. See
[egress guarantees](../security/egress-guarantees.md) for what the gateway does and does not
promise.

## Why an operation blocks

A `PolicyViolation` names the entity, the reason and a code. It never carries the value, so a
blocked operation can be diagnosed from a log the document never reached.

| Code | Why |
| --- | --- |
| `policy.blocked_entity` | A rule, or the default, said `Block` |
| `policy.review_required` | A rule said `RequireReview`. A human inside the trust boundary decides |
| `policy.unresolved_entity` | Something was found, not resolved, and is more sensitive than `UnresolvedTolerance` |
| `policy.incomplete_scan` | A part was not read or not scanned, so the document was never shown to be safe |
| `policy.too_many_entities` | The document holds more candidates than `Limits.MaxEntities` |

`RequireReview` blocks. It is not a warning: the operation stops and waits for a person.

## Why a policy fails to build

These are configuration errors. They throw `PolicyConfigurationException` from `Build()` or
`ToPolicy()` — at startup, where a mistake is cheap.

| Code | Why |
| --- | --- |
| `policy.id_invalid`, `policy.version_invalid` | Empty, over 128 characters, or containing a control character |
| `policy.action_invalid`, `policy.rule_action_invalid` | An action name the library does not define |
| `policy.scope_invalid`, `policy.rule_scope_invalid` | A scope name the library does not define |
| `policy.rule_scope_without_correlation` | A scope on an action that produces no correlatable token |
| `policy.rule_type_invalid` | An entity type that does not begin with a letter, or holds something other than letters, digits and underscores |
| `policy.rule_sensitivity_invalid` | A sensitivity level the library does not define |
| `policy.rule_missing` | A rule entry with nothing in it |
| `policy.unreachable_rule` | An earlier rule already matches everything this one would |
| `policy.tolerance_invalid` | A tolerance that is neither a sensitivity level nor `None` |
| `policy.thresholds_entry_invalid` | Thresholds keyed by something that is not an entity type |
| `policy.threshold_out_of_range` | A threshold outside 0.0 to 1.0 |
| `policy.flag_threshold_above_transform_threshold` | A flag threshold above the transform threshold, which would make the flag band empty |
| `policy.limit_not_positive` | A limit of zero or less |
| `policy.part_limit_exceeds_bundle_limit` | A per-part limit larger than the whole-bundle limit |

## What a policy cannot do

- It cannot turn a block into a warning. Fail-closed is not configurable.
- It cannot raise a candidate's sensitivity, or lower it.
- It cannot reach the pseudonym map. A policy decides that a value becomes a token; it never
  learns which token, and it never learns the way back.
- It cannot make the engine rewrite text. The engine produces a plan; the anonymizer acts on
  it.
