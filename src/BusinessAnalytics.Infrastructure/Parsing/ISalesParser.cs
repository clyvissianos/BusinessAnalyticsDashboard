using System.Threading;
using System.Threading.Tasks;

namespace BusinessAnalytics.Infrastructure.Parsing
{
    /// <summary>
    /// Parses a staged RawImport (CSV/XLS/XLSX) and loads rows into the warehouse.
    /// </summary>
    public interface ISalesParser
    {
        /// <summary>
        /// Parse and import the file associated with the given RawImport Id.
        /// Returns an ImportResult with rows imported and error details (if any).
        /// </summary>
        Task<ImportResult> ParseAndImportAsync(int importId, CancellationToken ct = default);
    }

    /// <summary>
    /// Result for a parsing/import operation.
    /// </summary>
    /// <param name="Rows">Number of fact rows successfully imported.</param>
    /// <param name="Success">True if completed without failing the import.</param>
    /// <param name="Error">Error message when Success is false (or null).</param>
    public readonly record struct ImportResult(int Rows, bool Success, string? Error);
}

