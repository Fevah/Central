using Central.Core.Integration;

namespace Central.Tests.Integration;

public class CsvImportAgentTests
{
    [Fact]
    public async Task TestConnection_NoPath_Fails()
    {
        var agent = new CsvImportAgent();
        await agent.InitializeAsync(new Dictionary<string, string>());
        var result = await agent.TestConnectionAsync();
        Assert.False(result.Success);
    }

    [Fact]
    public async Task TestConnection_MissingFile_Fails()
    {
        var agent = new CsvImportAgent();
        await agent.InitializeAsync(new Dictionary<string, string>
        {
            ["file_path"] = "/nonexistent/file_" + Guid.NewGuid() + ".csv"
        });
        var result = await agent.TestConnectionAsync();
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task Read_ValidCsv_ParsesRecords()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "name,email,age\nJohn,john@test.com,30\nJane,jane@test.com,25\n");

            var agent = new CsvImportAgent();
            await agent.InitializeAsync(new Dictionary<string, string>
            {
                ["file_path"] = path, ["delimiter"] = ","
            });

            var result = await agent.ReadAsync(new ReadRequest { MaxRecords = 100 });
            Assert.True(result.Success);
            Assert.Equal(2, result.Records.Count);
            Assert.Equal("John", result.Records[0]["name"]);
            Assert.Equal("jane@test.com", result.Records[1]["email"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Read_QuotedCsv_HandlesQuotes()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "name,description\n\"Smith, John\",\"Has a comma\"\n");

            var agent = new CsvImportAgent();
            await agent.InitializeAsync(new Dictionary<string, string> { ["file_path"] = path });

            var result = await agent.ReadAsync(new ReadRequest { MaxRecords = 100 });
            Assert.True(result.Success);
            Assert.Single(result.Records);
            Assert.Equal("Smith, John", result.Records[0]["name"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Read_TsvDelimiter()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "col1\tcol2\nA\tB\n");

            var agent = new CsvImportAgent();
            await agent.InitializeAsync(new Dictionary<string, string>
            {
                ["file_path"] = path, ["delimiter"] = "\t"
            });

            var result = await agent.ReadAsync(new ReadRequest { MaxRecords = 100 });
            Assert.True(result.Success);
            Assert.Equal("A", result.Records[0]["col1"]);
            Assert.Equal("B", result.Records[0]["col2"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Read_NoHeader_UsesColN()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "X,Y,Z\n");

            var agent = new CsvImportAgent();
            await agent.InitializeAsync(new Dictionary<string, string>
            {
                ["file_path"] = path, ["has_header"] = "false"
            });

            var result = await agent.ReadAsync(new ReadRequest { MaxRecords = 100 });
            Assert.True(result.Success);
            Assert.Contains("col_0", result.Records[0].Keys);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task GetEntityNames_ReturnsFileName()
    {
        var agent = new CsvImportAgent();
        await agent.InitializeAsync(new Dictionary<string, string>
        {
            ["file_path"] = "/tmp/users.csv"
        });

        var names = await agent.GetEntityNamesAsync();
        Assert.Contains("users", names);
    }

    [Fact]
    public async Task GetFields_ReturnsHeaders()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "name,email,role\n");

            var agent = new CsvImportAgent();
            await agent.InitializeAsync(new Dictionary<string, string> { ["file_path"] = path });

            var fields = await agent.GetFieldsAsync("data");
            Assert.Equal(3, fields.Count);
            Assert.Equal("name", fields[0].Name);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Write_ReturnsReadOnly()
    {
        var agent = new CsvImportAgent();
        var result = await agent.WriteAsync(new WriteRequest());
        Assert.False(result.Success);
        Assert.Contains("read-only", result.ErrorMessage);
    }
}
