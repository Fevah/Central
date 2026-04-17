namespace Central.Tests.Services;

/// <summary>Tests for CLI argument parsing logic (mirrored since Desktop can't be referenced from net10.0).</summary>
public class StartupArgsTests
{
    private static (string? Server, string? User, string? Password, string? AuthMethod, string? Dsn) Parse(string[] args)
    {
        string? server = null, user = null, password = null, authMethod = null, dsn = null;
        for (int i = 0; i < args.Length; i++)
        {
            var next = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "-s": case "--server": server = next; i++; break;
                case "-u": case "--user": user = next; i++; break;
                case "-p": case "--password": password = next; i++; break;
                case "-a": case "--auth-method": authMethod = next; i++; break;
                case "-d": case "--dsn": dsn = next; i++; break;
            }
        }
        return (server, user, password, authMethod, dsn);
    }

    [Fact]
    public void Parse_Empty()
    {
        var (s, u, p, a, d) = Parse(Array.Empty<string>());
        Assert.Null(s); Assert.Null(u); Assert.Null(p); Assert.Null(a); Assert.Null(d);
    }

    [Fact]
    public void Parse_Dsn()
    {
        var (_, _, _, _, dsn) = Parse(new[] { "--dsn", "Host=localhost" });
        Assert.Equal("Host=localhost", dsn);
    }

    [Fact]
    public void Parse_ShortFlags()
    {
        var (s, u, p, a, _) = Parse(new[] { "-s", "srv", "-u", "admin", "-p", "pass", "-a", "password" });
        Assert.Equal("srv", s); Assert.Equal("admin", u); Assert.Equal("pass", p); Assert.Equal("password", a);
    }

    [Fact]
    public void Parse_LongFlags()
    {
        var (s, _, _, a, _) = Parse(new[] { "--server", "db.local", "--auth-method", "offline" });
        Assert.Equal("db.local", s); Assert.Equal("offline", a);
    }

    [Fact]
    public void Parse_MixedFlags()
    {
        var (s, u, _, a, d) = Parse(new[] { "-s", "host", "--user", "admin", "-a", "windows", "--dsn", "DSN=test" });
        Assert.Equal("host", s); Assert.Equal("admin", u); Assert.Equal("windows", a); Assert.Equal("DSN=test", d);
    }
}
