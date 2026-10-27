namespace SensitiveDataGateway.Benchmarks;

/// <summary>
/// How much of a document looks like something worth finding.
/// </summary>
public enum CorpusShape
{
    /// <summary>
    /// Prose with no sensitive shape in it. Every scanner runs and finds nothing.
    /// </summary>
    Clean = 0,

    /// <summary>
    /// The reference case: annotated documents from the synthetic corpus, at the density an
    /// ordinary business document has.
    /// </summary>
    Realistic = 1,

    /// <summary>
    /// One annotated value per short line. This is the published worst case.
    /// </summary>
    Dense = 2,
}
