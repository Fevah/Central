using Central.Core.Services;

namespace Central.Tests.Tasks;

public class TaskFileParserTests
{
    [Fact]
    public void ParseCsv_HeadersAndRows()
    {
        var path = Path.GetTempFileName() + ".csv";
        File.WriteAllText(path, "Title,Status,Priority\nBuild login,Open,High\nFix bug,Done,Low\n");
        try
        {
            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(3, dt.Columns.Count);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal("Title", dt.Columns[0].ColumnName);
            Assert.Equal("Build login", dt.Rows[0]["Title"].ToString());
            Assert.Equal("Done", dt.Rows[1]["Status"].ToString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseCsv_EmptyFile_ReturnsEmptyTable()
    {
        var path = Path.GetTempFileName() + ".csv";
        File.WriteAllText(path, "");
        try
        {
            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(0, dt.Columns.Count);
            Assert.Equal(0, dt.Rows.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseCsv_HeaderOnly_NoRows()
    {
        var path = Path.GetTempFileName() + ".csv";
        File.WriteAllText(path, "Title,Status\n");
        try
        {
            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(2, dt.Columns.Count);
            Assert.Equal(0, dt.Rows.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseCsv_QuotedValues_Trimmed()
    {
        var path = Path.GetTempFileName() + ".csv";
        File.WriteAllText(path, "\"Title\",\"Status\"\n\"Build login\",\"Open\"\n");
        try
        {
            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal("Title", dt.Columns[0].ColumnName);
            Assert.Equal("Build login", dt.Rows[0]["Title"].ToString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseMsProjectXml_BasicTasks()
    {
        var path = Path.GetTempFileName() + ".xml";
        File.WriteAllText(path, @"<?xml version=""1.0""?>
<Project xmlns=""http://schemas.microsoft.com/project"">
  <Tasks>
    <Task><UID>1</UID><Name>Design</Name><WBS>1</WBS><Start>2026-04-01</Start><Finish>2026-04-05</Finish><Duration>PT40H0M0S</Duration><PercentComplete>50</PercentComplete><Priority>500</Priority><Milestone>0</Milestone></Task>
    <Task><UID>2</UID><Name>Build</Name><WBS>2</WBS><Start>2026-04-06</Start><Finish>2026-04-10</Finish><Duration>PT40H0M0S</Duration><PercentComplete>0</PercentComplete><Priority>700</Priority><Milestone>0</Milestone><PredecessorLink><PredecessorUID>1</PredecessorUID></PredecessorLink></Task>
    <Task><UID>3</UID><Name>Release</Name><WBS>3</WBS><Start>2026-04-10</Start><Finish>2026-04-10</Finish><Duration>PT0H0M0S</Duration><PercentComplete>0</PercentComplete><Priority>500</Priority><Milestone>1</Milestone></Task>
  </Tasks>
</Project>");
        try
        {
            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(3, dt.Rows.Count);
            Assert.Equal("Design", dt.Rows[0]["Title"].ToString());
            Assert.Equal("Build", dt.Rows[1]["Title"].ToString());
            Assert.Equal("Release", dt.Rows[2]["Title"].ToString());
            Assert.Equal("true", dt.Rows[2]["IsMilestone"].ToString());
            Assert.Equal("1", dt.Rows[1]["Predecessor"].ToString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseMsProjectXml_SkipsRow0()
    {
        var path = Path.GetTempFileName() + ".xml";
        File.WriteAllText(path, @"<?xml version=""1.0""?>
<Project xmlns=""http://schemas.microsoft.com/project"">
  <Tasks>
    <Task><UID>0</UID><Name>0</Name></Task>
    <Task><UID>1</UID><Name>Real Task</Name></Task>
  </Tasks>
</Project>");
        try
        {
            var dt = TaskFileParser.ParseFile(path);
            Assert.Single(dt.Rows);
            Assert.Equal("Real Task", dt.Rows[0]["Title"].ToString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseExcel_HeadersAndRows()
    {
        var path = Path.GetTempFileName() + ".xlsx";
        try
        {
            // Create a real xlsx with ClosedXML
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Tasks");
                ws.Cell(1, 1).Value = "Title";
                ws.Cell(1, 2).Value = "Status";
                ws.Cell(1, 3).Value = "Points";
                ws.Cell(2, 1).Value = "Build login";
                ws.Cell(2, 2).Value = "Open";
                ws.Cell(2, 3).Value = "5";
                ws.Cell(3, 1).Value = "Fix bug";
                ws.Cell(3, 2).Value = "Done";
                ws.Cell(3, 3).Value = "3";
                wb.SaveAs(path);
            }

            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(3, dt.Columns.Count);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal("Title", dt.Columns[0].ColumnName);
            Assert.Equal("Build login", dt.Rows[0]["Title"].ToString());
            Assert.Equal("Done", dt.Rows[1]["Status"].ToString());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseExcel_SkipsEmptyRows()
    {
        var path = Path.GetTempFileName() + ".xlsx";
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Data");
                ws.Cell(1, 1).Value = "Name";
                ws.Cell(2, 1).Value = "Item1";
                // Row 3 empty
                ws.Cell(4, 1).Value = "Item2";
                wb.SaveAs(path);
            }

            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(2, dt.Rows.Count); // empty row 3 skipped
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseExcel_DuplicateHeaders_Deduplicated()
    {
        var path = Path.GetTempFileName() + ".xlsx";
        try
        {
            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Sheet1");
                ws.Cell(1, 1).Value = "Name";
                ws.Cell(1, 2).Value = "Name";
                ws.Cell(2, 1).Value = "A";
                ws.Cell(2, 2).Value = "B";
                wb.SaveAs(path);
            }

            var dt = TaskFileParser.ParseFile(path);
            Assert.Equal(2, dt.Columns.Count);
            Assert.Equal("Name", dt.Columns[0].ColumnName);
            Assert.Equal("Name_1", dt.Columns[1].ColumnName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ParseFile_UnsupportedFormat_Throws()
    {
        var path = Path.GetTempFileName() + ".xyz";
        File.WriteAllText(path, "data");
        try
        {
            Assert.Throws<NotSupportedException>(() => TaskFileParser.ParseFile(path));
        }
        finally { File.Delete(path); }
    }
}
