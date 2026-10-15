# 0017. Let the container compose the gateway, and the caller pass the policy

- **Status:** Accepted
- **Date:** 2026-10-07
- **Deciders:** @iamjonatha

## Context

`SensitiveDataGateway.Extensions` exists to turn four hand-assembled stages into one line in
`Program.cs`. The question is what that line is allowed to decide on the application's behalf,
and the tempting answer is "everything", because that is what a registration usually does.

Every operation on the gateway takes a policy. That is deliberate: the policy is the document
that says what may leave, and an operation that reaches for an ambient one is an operation
whose governing rules are invisible at the point of use. A container that resolves the policy
for the gateway would undo that in a single line, and nothing in a later code review would
show which policy governed which call.

There is a second constraint. The package has to publish under native ahead-of-time
compilation with full trimming and no warning, because the library is meant to run in the kind
of service that is deployed as a self-contained binary. Two ordinary conveniences break that
outright: the reflection-based configuration binder, and any registration overload that takes
a type and constructs it through `ActivatorUtilities`. Both ask for metadata the trimmer has
already removed, and both fail at run time rather than at build time.

The third question is when a bad configuration is noticed. A policy file that no longer parses
is a deployment incident, and the difference between learning about it at startup and learning
about it on the first request is the difference between a rollback and an outage.

## Decision

**The container composes the gateway. The caller passes the policy.** `AddSensitiveDataGateway`
publishes three services: the `DocumentGateway`, the `ISensitiveDataGateway` that forwards to
it, and the `SanitizationPolicy` the registration was configured with. A consumer injects both
and hands the policy to the operation it governs. The gateway's API does not change, gains no
overload that omits the policy, and holds no policy of its own.

**The default policy is the strict one.** A registration that states nothing publishes
`SanitizationPolicy.Strict`. Choosing a weaker policy is a call that appears in a diff.

**Configuration is read by hand, key by key.** `PolicyConfiguration` names every key it
understands and converts every value with an invariant `TryParse`. Nothing reflects over a
policy type. What configuration can express is exactly what that file names, so the schema
cannot drift away from its documentation without the compiler noticing.

**Registration is eager.** The builder is run, and the policy is built, inside
`AddSensitiveDataGateway`. A malformed section throws `PolicyConfigurationException` on the
line that registers it, before the host is built. There is no `ValidateOnStart`, no
`IHostedService` and no dependency on `Microsoft.Extensions.Hosting.Abstractions`: the
validation happens earlier than any of those would run, and the stack trace points at the
registration rather than at a framework.

**A second registration of the same name is a programming error.** `AddSensitiveDataGateway`
throws `InvalidOperationException` naming the registration — "the unnamed sensitive data
gateway", or the name in quotes. It never names a policy file, a key or a value, because a
startup error is written to a log that is not inside the trust boundary.

**Several gateways coexist as keyed services.** `AddSensitiveDataGateway(name, configure)`
registers under a string key, resolved with `[FromKeyedServices]`. Nothing looks a gateway up
by a name computed at run time, because a name nobody reviewed selects a policy nobody
reviewed.

**Everything a gateway is made of is given as a value or a factory.** `WithExtractor`,
`WithDetector`, `WithAuditSink` and `WithPseudonymStore` take instances or
`Func<IServiceProvider, T>`. There is no `WithDetector<T>()` that constructs `T`.

## Consequences

A consumer writes two injections where one would have done, and passes the policy explicitly
on every call. That is the cost, and it buys a call site that states which rules governed it.

`samples/Aot` publishes with `PublishAot`, `TrimMode=full` and `TrimmerSingleWarn=false`, and
the build fails on any trim or AOT warning. The sample composes a container, binds a policy
from configuration and sanitizes a document, so the three operations most likely to need
trimmed metadata are the three the smoke test performs.

A configuration key the schema does not name is ignored rather than rejected, which is how
`IConfiguration` behaves everywhere else. A key the schema does name, holding a value of the
wrong shape, throws.

`Microsoft.Extensions.Options` is not referenced. Nothing in the package is an options object,
because an options object that is read lazily is a configuration error discovered late.

Adding a stage to the gateway means adding a `With...` method here. That is deliberate
friction: it keeps the list of things a container may construct short enough to read.

## Alternatives rejected

**Register the policy as a dependency of the gateway.** One injection instead of two, at the
cost of the thing this library is about. A call site would no longer say what governed it, and
a registration change in a different file would silently change what every call is allowed to
send.

**Bind configuration with `IConfiguration.Get<T>()`.** Half the code, and an `IL2026` warning
the moment the sample publishes. The reflection binder would also accept any property somebody
later adds to a policy description, which is how a schema stops matching its documentation.

**Use `ValidateOnStart` with `IOptions<T>`.** It needs `Microsoft.Extensions.Hosting`, it only
covers hosts that run the hosting startup path, and it reports the failure one layer away from
the code that caused it. Building the policy during registration is earlier, simpler and has
no dependency.

**Generic registration overloads, `AddSensitiveDataGateway<TDetector>()`.** Familiar, and
immediately incompatible with the AOT guarantee. A detector is cheap to construct by hand, and
a factory expresses the same thing with no metadata requirement.

**Resolve named gateways through a factory interface,
`ISensitiveDataGatewayFactory.Get(string)`.** It permits a name assembled at run time from a
request, and therefore a path where the weakest registered policy is reachable from user
input. Keyed services make the choice a compile-time one.
