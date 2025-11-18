using BusinessAnalytics.Infrastructure.Parsing.Inference;
using ExcelDataReader;
using System.Data;
using System.Globalization;

namespace BusinessAnalytics.Infrastructure.Parsing.Preview;

public record PreviewResponse(
    string FileType,
    string[] Sheets,
    string SelectedSheet,
    string[] Headers,
    List<Dictionary<string, object?>> SampleRows,
    Dictionary<string, string?> SuggestedMap
);

public interface IPreviewService
{
    Task<PreviewResponse> PreviewAsync(string filePath, string? sheet = null, int sample = 20);
}

public class PreviewService : IPreviewService
{
    public async Task<PreviewResponse> PreviewAsync(string filePath, string? sheet = null, int sample = 20)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".csv")
        {
            // very light CSV header + sample reader
            using var sr = new StreamReader(filePath);
            var headLine = await sr.ReadLineAsync() ?? "";
            var headers = headLine.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None);
            var rows = new List<Dictionary<string, object?>>();
            for (int i = 0; i < sample && !sr.EndOfStream; i++)
            {
                var line = await sr.ReadLineAsync();
                if (line is null) break;
                var vals = line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None);
                var dict = new Dictionary<string, object?>();
                for (int c = 0; c < headers.Length; c++)
                    dict[headers[c]] = c < vals.Length ? vals[c] : null;
                rows.Add(dict);
            }
            var suggested = ColumnInference.SuggestMap(headers);
            return new PreviewResponse("csv", Array.Empty<string>(), "", headers, rows, suggested);
        }
        else if (ext is ".xlsx" or ".xls")
        {
            using var fs = File.OpenRead(filePath);
            using var reader = ExcelReaderFactory.CreateReader(fs);
            var ds = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            var sheetName = sheet ?? ds.Tables[0].TableName;
            var table = ds.Tables[sheetName];

            var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
            var rows = new List<Dictionary<string, object?>>();
            for (int r = 0; r < Math.Min(sample, table.Rows.Count); r++)
            {
                var dict = new Dictionary<string, object?>();
                for (int c = 0; c < table.Columns.Count; c++)
                    dict[headers[c]] = table.Rows[r][c];
                rows.Add(dict);
            }

            var suggested = ColumnInference.SuggestMap(headers);
            return new PreviewResponse(
                ext.TrimStart('.'),
                ds.Tables.Cast<DataTable>().Select(t => t.TableName).ToArray(),
                sheetName,
                headers,
                rows,
                suggested
            );
        }
        else
        {
            throw new NotSupportedException($"Unsupported file type: {ext}");
        }
    }
}

