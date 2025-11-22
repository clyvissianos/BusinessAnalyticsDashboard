using BusinessAnalytics.Domain.Entities;
using BusinessAnalytics.Infrastructure.Parsing.Inference;
using BusinessAnalytics.Infrastructure.Persistence;
using CsvHelper;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using System.Formats.Asn1;
using System.Globalization;
using System.Text.Json;

namespace BusinessAnalytics.Infrastructure.Parsing;

public class SalesParser : ISalesParser
{
    //private static readonly string[] Canonical = ["Date", "Product", "Customer", "Quantity", "Amount"];
    private static readonly string[] Required = ["Date", "Product", "Customer", "Amount"];

    private readonly AppDbContext _db;

    public SalesParser(AppDbContext db) => _db = db;

    public async Task<ImportResult> ParseAndImportAsync(int importId, CancellationToken ct = default)
    {
        var import = await _db.RawImports
            .Include(r => r.DataSource)
            .FirstOrDefaultAsync(r => r.Id == importId, ct);

        if (import is null) return new(0, false, "Import not found.");
        if (import.Status != ImportStatus.Staged) return new(0, false, $"Invalid status: {import.Status}.");

        // Load mapping (if any)
        var mapping = await _db.DataSourceMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.DataSourceId == import.DataSourceId, ct);

        var cultureName = mapping?.Culture?.Trim().Length > 0 ? mapping.Culture : "el-GR";
        var culture = new CultureInfo(cultureName);

        try
        {
            var ext = Path.GetExtension(import.OriginalFilePath).ToLowerInvariant();

            // Read headers + row enumerator factory based on file type
            string[] headers;
            Func<IAsyncEnumerable<Dictionary<string, object?>>> rowStreamFactory;

            if (ext is ".csv")
            {
                (headers, rowStreamFactory) = InitCsv(import.OriginalFilePath, culture);
            }
            else if (ext is ".xlsx" or ".xls")
            {
                (headers, rowStreamFactory) = InitExcel(import.OriginalFilePath, mapping?.SheetName);
            }
            else
            {
                return await Fail(import, $"Unsupported file type: {ext}", ct);
            }

            // Determine mapping canonical->sourceHeader
            var headerMap = ResolveHeaderMap(headers, mapping);

            // Validate mapping completeness
            var missing = Required.Where(k => !headerMap.ContainsKey(k) || string.IsNullOrWhiteSpace(headerMap[k]!)).ToList();
            if (missing.Count > 0)
            {
                var msg = $"Missing column mapping for: {string.Join(", ", missing)}.";
                return await Fail(import, msg, ct);
            }

            // Parse rows
            var rows = 0;
            var errCount = 0;
            var errSamples = new List<string>();

            await foreach (var row in rowStreamFactory())
            {
                try
                {
                    var dateVal = GetString(row, headerMap["Date"]!);
                    var product = GetString(row, headerMap["Product"]!);
                    var customer = GetString(row, headerMap["Customer"]!);
                    // Quantity is optional – default to "1" if not mapped
                    string qtyVal;
                    if (headerMap.TryGetValue("Quantity", out var qtyCol) && !string.IsNullOrWhiteSpace(qtyCol))
                        qtyVal = GetString(row, qtyCol);
                    else
                        qtyVal = "1";

                    var amountVal = GetString(row, headerMap["Amount"]!);

                    if (!TryParseDate(dateVal, culture, out var d)) throw new FormatException($"Invalid Date: '{dateVal}'");

                    int qty;
                    if (!int.TryParse(qtyVal, NumberStyles.Any, culture, out qty))
                    {
                        // allow invariant fallback (e.g., "1,000" vs "1000")
                        if (!int.TryParse(qtyVal, NumberStyles.Any, CultureInfo.InvariantCulture, out qty))
                            qty = 1;
                        //throw new FormatException($"Invalid Quantity: '{qtyVal}'");
                    }

                    // Try culture first (el-GR by default), then invariant as fallback
                    if (!decimal.TryParse(amountVal, NumberStyles.Any, culture, out var amount))
                    {
                        if (!decimal.TryParse(amountVal, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                            throw new FormatException($"Invalid Amount: '{amountVal}'");
                    }

                    var dateKey = DimDate.ToDateKey(d);
                    var productId = await GetOrCreateProductAsync(product, ct);
                    var customerId = await GetOrCreateCustomerAsync(customer, ct);

                    _db.FactSales.Add(new FactSales
                    {
                        DataSourceId = import.DataSourceId,   // ✅ link to parent datasource

                        DateKey = dateKey,
                        ProductKey = productId,
                        CustomerKey = customerId,
                        Quantity = qty,
                        Amount = amount
                    });

                    rows++;
                    if (rows % 1000 == 0) await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    errCount++;
                    if (errSamples.Count < 10)
                        errSamples.Add(ex.Message);
                }
            }

            await _db.SaveChangesAsync(ct);

            // Decide success vs fail by error rate (tolerate a few)
            var totalRows = rows + errCount;
            var rate = totalRows == 0 ? 1.0 : (double)errCount / totalRows;
            if (rate > 0.05) // >5% errors → fail
            {
                var msg = $"Parsing aborted. Error rate {(rate * 100):0.0}% (rows={rows}, errors={errCount}). " +
                          $"Samples: {string.Join(" | ", errSamples)}";
                return await Fail(import, msg, ct);
            }

            import.Status = ImportStatus.Parsed;
            import.Rows = rows;
            import.CompletedAtUtc = DateTime.UtcNow;
            import.ErrorMessage = errCount > 0 ? $"Completed with {errCount} row errors." : null;
            await _db.SaveChangesAsync(ct);

            return new(rows, true, null);
        }
        catch (Exception ex)
        {
            return await Fail(import, ex.Message, ct);
        }
    }

    // ---------------- helpers ----------------

    private static (string[] headers, Func<IAsyncEnumerable<Dictionary<string, object?>>> read)
        InitCsv(string path, CultureInfo culture)
    {
        return (ReadCsvHeaders(path),
            () => StreamCsv(path, culture));
    }

    private static string[] ReadCsvHeaders(string path)
    {
        using var sr = new StreamReader(path);
        var first = sr.ReadLine() ?? "";
        // accept comma/semicolon/tab
        var headers = first.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None);
        return headers;
    }

    private static async IAsyncEnumerable<Dictionary<string, object?>> StreamCsv(string path, CultureInfo culture)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(culture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true,
            TrimOptions = CsvHelper.Configuration.TrimOptions.Trim,
            BadDataFound = null
        });

        await foreach (var record in csv.GetRecordsAsync<dynamic>())
        {
            // Convert to Dictionary<string, object?>
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in (IDictionary<string, object?>)record)
                dict[kv.Key] = kv.Value;
            yield return dict;
        }
    }

    private static (string[] headers, Func<IAsyncEnumerable<Dictionary<string, object?>>> read)
        InitExcel(string path, string? sheet)
    {
        return (ReadExcelHeaders(path, sheet, out var chosenSheet),
            () => StreamExcel(path, chosenSheet));
    }

    private static string[] ReadExcelHeaders(string path, string? sheet, out string chosenSheet)
    {
        using var fs = File.OpenRead(path);
        using var reader = ExcelReaderFactory.CreateReader(fs);
        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var table = sheet is not null && ds.Tables.Contains(sheet) ? ds.Tables[sheet] : ds.Tables[0];
        chosenSheet = table.TableName;
        var headers = table.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToArray();
        return headers;
    }

    private static async IAsyncEnumerable<Dictionary<string, object?>> StreamExcel(string path, string sheet)
    {
        using var fs = File.OpenRead(path);
        using var reader = ExcelReaderFactory.CreateReader(fs);
        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
        });

        var table = ds.Tables[sheet];
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var header = table.Columns[c].ColumnName;
                dict[header] = table.Rows[r][c];
            }
            yield return dict;
            await Task.Yield();
        }
    }

    private static Dictionary<string, string?> ResolveHeaderMap(string[] headers, DataSourceMapping? mapping)
    {
        // If saved mapping exists and covers all canonical fields → use it
        if (mapping is not null && !string.IsNullOrWhiteSpace(mapping.ColumnMapJson))
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(mapping.ColumnMapJson)
                      ?? new Dictionary<string, string>();
            if (Required.All(k => map.ContainsKey(k) && !string.IsNullOrWhiteSpace(map[k])))
                return map;
        }

        // Otherwise infer
        var suggested = ColumnInference.SuggestMap(headers);
        return suggested;
    }

    private static bool TryParseDate(string? s, CultureInfo culture, out DateOnly d)
    {
        d = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        // Try common formats first
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"];
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(s, fmt, culture, DateTimeStyles.None, out var dt))
            {
                d = DateOnly.FromDateTime(dt);
                return true;
            }
            if (DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                d = DateOnly.FromDateTime(dt);
                return true;
            }
        }

        // Fallback to generic parse
        if (DateTime.TryParse(s, culture, DateTimeStyles.None, out var any))
        {
            d = DateOnly.FromDateTime(any);
            return true;
        }
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out any))
        {
            d = DateOnly.FromDateTime(any);
            return true;
        }
        return false;
    }

    private static string GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v is null) return "";
        return v switch
        {
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => v.ToString() ?? ""
        };
    }

    private async Task<ImportResult> Fail(RawImport import, string message, CancellationToken ct)
    {
        import.Status = ImportStatus.Failed;
        import.ErrorMessage = message;
        import.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new(0, false, message);
    }

    private async Task<int> GetOrCreateProductAsync(string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name)) name = "(unknown)";
        var existing = await _db.DimProducts.FirstOrDefaultAsync(p => p.ProductName == name, ct);
        if (existing is not null) return existing.ProductKey;
        var p = new DimProduct { ProductName = name };
        _db.DimProducts.Add(p);
        await _db.SaveChangesAsync(ct);
        return p.ProductKey;
    }

    private async Task<int> GetOrCreateCustomerAsync(string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name)) name = "(unknown)";
        var existing = await _db.DimCustomers.FirstOrDefaultAsync(c => c.CustomerName == name, ct);
        if (existing is not null) return existing.CustomerKey;
        var c = new DimCustomer { CustomerName = name };
        _db.DimCustomers.Add(c);
        await _db.SaveChangesAsync(ct);
        return c.CustomerKey;
    }
}

