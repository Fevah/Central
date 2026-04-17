using System.IO;

namespace Central.Engine.Integration;

/// <summary>
/// Integration agent that reads CSV/TSV files.
/// Config: { "file_path": "C:/data/import.csv", "delimiter": ",", "has_header": "true", "encoding": "utf-8" }
/// Supports: local file paths and UNC paths.
/// </summary>
public class CsvImportAgent : IIntegrationAgent
{
    private string _filePath = "";
    private string _delimiter = ",";
    private bool _hasHeader = true;
    private System.Text.Encoding _encoding = System.Text.Encoding.UTF8;

    public string AgentType => "csv_import";
    public string DisplayName => "CSV File Import";

    public Task InitializeAsync(Dictionary<string, string> config)
    {
        _filePath = config.GetValueOrDefault("file_path", "");
        _delimiter = config.GetValueOrDefault("delimiter", ",");
        _hasHeader = config.GetValueOrDefault("has_header", "true") != "false";
        var enc = config.GetValueOrDefault("encoding", "utf-8");
        _encoding = enc.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => System.Text.Encoding.UTF8,
            "ascii" => System.Text.Encoding.ASCII,
            "latin1" or "iso-8859-1" => System.Text.Encoding.Latin1,
            _ => System.Text.Encoding.UTF8
        };
        return Task.CompletedTask;
    }

    public Task<AgentTestResult> TestConnectionAsync()
    {
        if (string.IsNullOrEmpty(_filePath))
            return Task.FromResult(AgentTestResult.Fail("No file_path configured"));
        if (!File.Exists(_filePath))
            return Task.FromResult(AgentTestResult.Fail($"File not found: {_filePath}"));

        var lines = File.ReadLines(_filePath, _encoding).Take(2).ToList();
        var cols = lines.Count > 0 ? lines[0].Split(_delimiter).Length : 0;
        return Task.FromResult(AgentTestResult.Ok($"File exists — {cols} columns detected"));
    }

    public Task<AgentReadResult> ReadAsync(ReadRequest request)
    {
        var records = new List<Dictionary<string, object?>>();

        try
        {
            if (!File.Exists(_filePath))
                return Task.FromResult(new AgentReadResult { Success = false, ErrorMessage = $"File not found: {_filePath}" });

            var lines = File.ReadAllLines(_filePath, _encoding);
            if (lines.Length == 0)
                return Task.FromResult(new AgentReadResult { Success = true, Records = records });

            string[] headers;
            int startLine;

            if (_hasHeader)
            {
                headers = ParseLine(lines[0]);
                startLine = 1;
            }
            else
            {
                var colCount = ParseLine(lines[0]).Length;
                headers = Enumerable.Range(0, colCount).Select(i => $"col_{i}").ToArray();
                startLine = 0;
            }

            for (int i = startLine; i < lines.Length && records.Count < request.MaxRecords; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseLine(line);
                var record = new Dictionary<string, object?>();
                for (int j = 0; j < headers.Length && j < values.Length; j++)
                {
                    var val = values[j].Trim();
                    record[headers[j].Trim()] = string.IsNullOrEmpty(val) ? null : val;
                }
                records.Add(record);
            }

            return Task.FromResult(new AgentReadResult
            {
                Success = true,
                Records = records,
                TotalAvailable = records.Count
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new AgentReadResult { Success = false, ErrorMessage = ex.Message });
        }
    }

    public Task<AgentWriteResult> WriteAsync(WriteRequest request)
        => Task.FromResult(AgentWriteResult.Fail("CSV agent is read-only"));

    public Task<AgentWriteResult> DeleteAsync(string entityName, string externalId)
        => Task.FromResult(AgentWriteResult.Fail("CSV agent is read-only"));

    public Task<List<string>> GetEntityNamesAsync()
    {
        var name = Path.GetFileNameWithoutExtension(_filePath);
        return Task.FromResult(new List<string> { string.IsNullOrEmpty(name) ? "data" : name });
    }

    public Task<List<AgentFieldInfo>> GetFieldsAsync(string entityName)
    {
        if (!File.Exists(_filePath))
            return Task.FromResult(new List<AgentFieldInfo>());

        var firstLine = File.ReadLines(_filePath, _encoding).FirstOrDefault();
        if (firstLine == null)
            return Task.FromResult(new List<AgentFieldInfo>());

        var headers = _hasHeader
            ? ParseLine(firstLine)
            : Enumerable.Range(0, ParseLine(firstLine).Length).Select(i => $"col_{i}").ToArray();

        return Task.FromResult(headers.Select(h => new AgentFieldInfo
        {
            Name = h.Trim(),
            Type = "string",
            IsReadOnly = true
        }).ToList());
    }

    private string[] ParseLine(string line)
    {
        if (_delimiter == "," && line.Contains('"'))
            return ParseCsvWithQuotes(line);
        return line.Split(_delimiter);
    }

    private static string[] ParseCsvWithQuotes(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
