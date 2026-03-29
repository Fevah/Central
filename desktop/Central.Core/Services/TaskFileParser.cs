using System.Data;
using System.IO;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace Central.Core.Services;

/// <summary>Parses Excel (.xlsx), CSV, and MS Project XML files into DataTable for import.</summary>
public static class TaskFileParser
{
    public static DataTable ParseFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".csv" => ParseCsv(filePath),
            ".xml" => ParseMsProjectXml(filePath),
            ".xlsx" or ".xls" => ParseExcel(filePath),
            _ => throw new NotSupportedException($"Unsupported file format: {ext}")
        };
    }

    /// <summary>Parse Excel file using ClosedXML — reads first worksheet, header row + data.</summary>
    private static DataTable ParseExcel(string filePath)
    {
        var dt = new DataTable("Import");
        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheets.First();
        var range = ws.RangeUsed();
        if (range == null) return dt;

        var rowCount = range.RowCount();
        var colCount = range.ColumnCount();
        if (rowCount == 0 || colCount == 0) return dt;

        // Header row (row 1)
        for (int c = 1; c <= colCount; c++)
        {
            var header = ws.Cell(1, c).GetString().Trim();
            if (string.IsNullOrEmpty(header)) header = $"Column{c}";
            var name = header;
            var suffix = 1;
            while (dt.Columns.Contains(name)) name = $"{header}_{suffix++}";
            dt.Columns.Add(name);
        }

        // Data rows (row 2+)
        for (int r = 2; r <= rowCount; r++)
        {
            var row = dt.NewRow();
            var hasData = false;
            for (int c = 1; c <= colCount; c++)
            {
                var cell = ws.Cell(r, c);
                var val = cell.IsEmpty() ? "" : cell.GetString().Trim();
                row[c - 1] = val;
                if (!string.IsNullOrEmpty(val)) hasData = true;
            }
            if (hasData) dt.Rows.Add(row);
        }

        return dt;
    }

    private static DataTable ParseCsv(string filePath)
    {
        var dt = new DataTable("Import");
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0) return dt;

        var headers = lines[0].Split(',');
        foreach (var h in headers)
            dt.Columns.Add(h.Trim().Trim('"'));

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = lines[i].Split(',');
            var row = dt.NewRow();
            for (int j = 0; j < Math.Min(values.Length, dt.Columns.Count); j++)
                row[j] = values[j].Trim().Trim('"');
            dt.Rows.Add(row);
        }
        return dt;
    }

    /// <summary>Parse MS Project XML format — tasks with WBS, dates, dependencies.</summary>
    private static DataTable ParseMsProjectXml(string filePath)
    {
        var dt = new DataTable("Import");
        dt.Columns.Add("Title");
        dt.Columns.Add("WBS");
        dt.Columns.Add("StartDate");
        dt.Columns.Add("FinishDate");
        dt.Columns.Add("Duration");
        dt.Columns.Add("PercentComplete");
        dt.Columns.Add("Priority");
        dt.Columns.Add("IsMilestone");
        dt.Columns.Add("Predecessor");

        try
        {
            var doc = XDocument.Load(filePath);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var tasks = doc.Descendants(ns + "Task");

            foreach (var task in tasks)
            {
                var name = task.Element(ns + "Name")?.Value ?? "";
                if (string.IsNullOrWhiteSpace(name) || name == "0") continue;

                var row = dt.NewRow();
                row["Title"] = name;
                row["WBS"] = task.Element(ns + "WBS")?.Value ?? "";
                row["StartDate"] = task.Element(ns + "Start")?.Value ?? "";
                row["FinishDate"] = task.Element(ns + "Finish")?.Value ?? "";
                row["Duration"] = task.Element(ns + "Duration")?.Value ?? "";
                row["PercentComplete"] = task.Element(ns + "PercentComplete")?.Value ?? "0";
                row["Priority"] = task.Element(ns + "Priority")?.Value ?? "500";
                row["IsMilestone"] = task.Element(ns + "Milestone")?.Value == "1" ? "true" : "false";

                var preds = task.Descendants(ns + "PredecessorLink");
                var predIds = preds.Select(p => p.Element(ns + "PredecessorUID")?.Value).Where(v => v != null);
                row["Predecessor"] = string.Join(";", predIds);

                dt.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse MS Project XML: {ex.Message}", ex);
        }

        return dt;
    }
}
