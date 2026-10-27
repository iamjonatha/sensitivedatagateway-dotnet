using System.Reflection;
using BenchmarkDotNet.Running;

namespace SensitiveDataGateway.Benchmarks;

/// <summary>
/// Runs the benchmarks.
/// </summary>
/// <remarks>
/// <c>dotnet run -c Release --project benchmarks/SensitiveDataGateway.Benchmarks</c> runs
/// everything; <c>--filter *Detect*</c> narrows it. The published figures are in
/// <c>docs/performance.md</c>.
/// </remarks>
public static class Program
{
    /// <summary>
    /// The entry point.
    /// </summary>
    /// <param name="args">The arguments BenchmarkDotNet parses.</param>
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
}
