namespace Central.Tests.Enterprise;

/// <summary>
/// Tests for integrity result model patterns.
/// Note: Central.Protection is net10.0-windows only — we test result model logic inline.
/// </summary>
public class IntegrityResultTests
{
    private class IntegrityResult
    {
        public bool IsIntact { get; set; }
        public List<string> VerifiedFiles { get; } = new();
        public List<string> TamperedFiles { get; } = new();
        public List<string> MissingFiles { get; } = new();
        public string Summary => IsIntact
            ? $"Integrity OK — {VerifiedFiles.Count} files verified"
            : $"INTEGRITY VIOLATION — {TamperedFiles.Count} tampered, {MissingFiles.Count} missing";
    }

    [Fact]
    public void IsIntact_WhenNoIssues()
    {
        var result = new IntegrityResult { IsIntact = true };
        result.VerifiedFiles.Add("Central.Engine.dll");
        result.VerifiedFiles.Add("Central.Persistence.dll");

        Assert.True(result.IsIntact);
        Assert.Equal(2, result.VerifiedFiles.Count);
        Assert.Empty(result.TamperedFiles);
        Assert.Empty(result.MissingFiles);
    }

    [Fact]
    public void Summary_WhenIntact()
    {
        var result = new IntegrityResult { IsIntact = true };
        result.VerifiedFiles.Add("A.dll");
        result.VerifiedFiles.Add("B.dll");
        Assert.Equal("Integrity OK — 2 files verified", result.Summary);
    }

    [Fact]
    public void Summary_WhenTampered()
    {
        var result = new IntegrityResult { IsIntact = false };
        result.TamperedFiles.Add("Central.Engine.dll");
        Assert.Contains("INTEGRITY VIOLATION", result.Summary);
        Assert.Contains("1 tampered", result.Summary);
        Assert.Contains("0 missing", result.Summary);
    }

    [Fact]
    public void Summary_WhenMissing()
    {
        var result = new IntegrityResult { IsIntact = false };
        result.MissingFiles.Add("Central.Missing.dll");
        Assert.Contains("1 missing", result.Summary);
    }

    [Fact]
    public void Summary_BothTamperedAndMissing()
    {
        var result = new IntegrityResult { IsIntact = false };
        result.TamperedFiles.Add("A.dll");
        result.TamperedFiles.Add("B.dll");
        result.MissingFiles.Add("C.dll");
        Assert.Contains("2 tampered", result.Summary);
        Assert.Contains("1 missing", result.Summary);
    }

    [Fact]
    public void Defaults()
    {
        var result = new IntegrityResult();
        Assert.False(result.IsIntact);
        Assert.Empty(result.VerifiedFiles);
        Assert.Empty(result.TamperedFiles);
        Assert.Empty(result.MissingFiles);
    }

    [Fact]
    public void Summary_ZeroVerified_WhenIntact()
    {
        var result = new IntegrityResult { IsIntact = true };
        Assert.Equal("Integrity OK — 0 files verified", result.Summary);
    }
}
