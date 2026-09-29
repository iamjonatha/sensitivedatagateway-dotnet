using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Core.Tests;

/// <summary>
/// Asserts the properties the trust boundary is supposed to have, by reflection rather than by
/// review. Each of these could be broken by a plausible, well-meant change.
/// </summary>
public sealed class TrustBoundaryTests
{
    private static readonly Type[] s_documentTypes =
    [
        typeof(DocumentBundle),
        typeof(DocumentPart),
        typeof(DocumentContent),
        typeof(DocumentMetadata),
        typeof(TextSegment),
        typeof(ExtractedDocument),
        typeof(DetectedDocument),
        typeof(SensitiveEntity),
        typeof(SanitizedDocument),
    ];

    [Fact]
    public void DecisionProvider_AcceptsNothingThatCanCarryRawContent()
    {
        var offenders = new List<string>();

        foreach (var member in typeof(IDecisionProvider<,>).GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var type in SignatureTypes(member))
            {
                if (s_documentTypes.Contains(type))
                {
                    offenders.Add($"{member.Name} mentions {type.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void DecisionProvider_TakesTheValidatedPayloadAsItsFirstArgument()
    {
        var method = typeof(IDecisionProvider<,>).GetMethod("DecideAsync");

        Assert.NotNull(method);

        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(ValidatedPayload), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
    }

    [Fact]
    public void ValidatedPayload_HasNoPublicConstructor()
    {
        var constructors = typeof(ValidatedPayload).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(constructors);
    }

    [Fact]
    public void ValidatedPayload_HasNoPublicFactory()
    {
        var factories = typeof(ValidatedPayload)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(method => method.ReturnType == typeof(ValidatedPayload))
            .Select(method => method.Name)
            .ToArray();

        Assert.Empty(factories);
    }

    [Fact]
    public void ValidatedPayload_CannotBeConstructedReflectivelyThroughAPublicSurface() =>
        Assert.Throws<MissingMethodException>(() => Activator.CreateInstance<ValidatedPayload>());

    [Fact]
    public void ValidatedPayload_ExposesNothingThatCanCarryRawContent()
    {
        var offenders = typeof(ValidatedPayload)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(SignatureTypes)
            .Where(s_documentTypes.Contains)
            .Select(type => type.Name)
            .Distinct()
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Exceptions_ExposeNothingThatCanCarryRawContent()
    {
        var offenders = new List<string>();

        foreach (var exceptionType in ExceptionTypes())
        {
            foreach (var property in exceptionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (s_documentTypes.Contains(property.PropertyType))
                {
                    offenders.Add($"{exceptionType.Name}.{property.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Exceptions_AreOneLevelDeep()
    {
        var offenders = ExceptionTypes()
            .Where(type => type.BaseType != typeof(SensitiveDataGatewayException))
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Exceptions_AreSealed()
    {
        var offenders = ExceptionTypes()
            .Where(type => !type.IsSealed)
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void PolicyViolation_HasNoExceptionType()
    {
        var offenders = ExceptionTypes()
            .Where(type => type.Name.Contains("PolicyViolation", StringComparison.Ordinal))
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> ExceptionTypes() =>
        typeof(SensitiveDataGatewayException).Assembly
            .GetExportedTypes()
            .Where(type => type != typeof(SensitiveDataGatewayException))
            .Where(typeof(SensitiveDataGatewayException).IsAssignableFrom);

    private static IEnumerable<Type> SignatureTypes(MemberInfo member)
    {
        switch (member)
        {
            case MethodInfo method:
                yield return method.ReturnType;

                foreach (var parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;

            case PropertyInfo property:
                yield return property.PropertyType;
                break;

            case ConstructorInfo constructor:
                foreach (var parameter in constructor.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;
        }
    }
}
