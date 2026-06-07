using System.Text.Json;

namespace ShellKrypt.Infrastructure.Vaulting;

internal static partial class VaultCsvImportParser
{
    private const int MaxCsvRows = 10000;
    private const int MaxCsvColumns = 64;
    private const int MaxCsvFieldChars = 16384;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 64
    };

    public static List<CsvCandidate> ParseCandidates(string csvText)
    {
        var records = ParseRecords(csvText);
        if (records.Count == 0)
            return [];

        var headers = records[0];
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i].Trim();
            if (!string.IsNullOrWhiteSpace(header) && !index.ContainsKey(header))
                index[header] = i;
        }

        var candidates = new List<CsvCandidate>();
        for (var rowIndex = 1; rowIndex < records.Count; rowIndex++)
        {
            var candidate = ParseCandidate(records[rowIndex], index, rowIndex + 1);
            if (candidate is not null)
                candidates.Add(candidate);
        }

        return candidates;
    }
}
