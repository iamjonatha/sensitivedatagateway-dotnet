using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// A value a detector found in the original, as the scanner needs to see it.
/// </summary>
/// <param name="Value">The text the detector pointed at.</param>
/// <param name="Type">What the detector said it was.</param>
/// <remarks>
/// This type is internal, and stays internal. It is the one structure in the project that
/// holds sensitive values next to their classification, which is the shape of the pseudonym
/// map the boundary exists to contain. It lives for the duration of one scan.
/// </remarks>
internal readonly record struct LeakCandidate(string Value, EntityType Type);
