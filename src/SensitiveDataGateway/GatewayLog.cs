using System;
using Microsoft.Extensions.Logging;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Auditing;

namespace SensitiveDataGateway;

/// <summary>
/// Everything the gateway says out loud.
/// </summary>
/// <remarks>
/// <para>
/// No parameter here is a <see cref="string"/>, and that is enforced by a test rather than by
/// discipline. A string parameter is how document content reaches a log: someone needs a
/// detail, the nearest string is the text, and the log file becomes a copy of the document
/// with none of its protections.
/// </para>
/// <para>
/// What is left — counts, identifiers the application chose, enumerations and durations — is
/// enough to tell what happened and never enough to tell what it said. An operator who needs
/// the error code reads the audit record, which is the artefact built for that question.
/// </para>
/// </remarks>
internal static partial class GatewayLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Operation {OperationId} started: {Operation} on bundle {BundleId} under policy {PolicyId} {PolicyVersion}.")]
    internal static partial void Started(
        ILogger logger,
        Guid operationId,
        AuditOperation operation,
        BundleId bundleId,
        PolicyId policyId,
        PolicyVersion policyVersion);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Operation {OperationId} extracted {SegmentCount} segments and {CharacterCount} characters, with {FailureCount} parts unread.")]
    internal static partial void Extracted(
        ILogger logger,
        Guid operationId,
        int segmentCount,
        long characterCount,
        int failureCount);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Operation {OperationId} found {EntityCount} candidates, with {FailureCount} segments unscanned.")]
    internal static partial void Detected(ILogger logger, Guid operationId, int entityCount, int failureCount);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Operation {OperationId} planned {DecisionCount} transformations and left {UnresolvedCount} unresolved; blocked: {IsBlocked}.")]
    internal static partial void Planned(
        ILogger logger,
        Guid operationId,
        int decisionCount,
        int unresolvedCount,
        bool isBlocked);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "Operation {OperationId} applied {AppliedCount} transformations with status {Status} and {WarningCount} warnings.")]
    internal static partial void Sanitized(
        ILogger logger,
        Guid operationId,
        int appliedCount,
        SanitizationStatus status,
        int warningCount);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Debug,
        Message = "Operation {OperationId} validation passed: {Passed}, with {FindingCount} findings.")]
    internal static partial void Validated(ILogger logger, Guid operationId, bool passed, int findingCount);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Operation {OperationId} allowed bundle {BundleId} after {Duration}.")]
    internal static partial void Allowed(ILogger logger, Guid operationId, BundleId bundleId, TimeSpan duration);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Operation {OperationId} blocked bundle {BundleId} on {FailedCheckCount} checks after {Duration}.")]
    internal static partial void Blocked(
        ILogger logger,
        Guid operationId,
        BundleId bundleId,
        int failedCheckCount,
        TimeSpan duration);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "Operation {OperationId} failed on bundle {BundleId} in category {ErrorCategory} after {Duration}.")]
    internal static partial void Failed(
        ILogger logger,
        Guid operationId,
        BundleId bundleId,
        GatewayErrorCategory? errorCategory,
        TimeSpan duration);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Warning,
        Message = "Operation {OperationId} was cancelled on bundle {BundleId} after {Duration}.")]
    internal static partial void Cancelled(ILogger logger, Guid operationId, BundleId bundleId, TimeSpan duration);
}
