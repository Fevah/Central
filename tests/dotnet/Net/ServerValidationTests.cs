using Central.Engine.Net.Dialogs;
using Central.Engine.Net.Servers;
using static Central.Engine.Net.Dialogs.ServerValidation;

namespace Central.Tests.Net;

public class ServerValidationTests
{
    // ── ServerProfile ────────────────────────────────────────────

    [Fact]
    public void Profile_HappyPath_OK()
    {
        var p = new ServerProfile
        {
            ProfileCode = "Server4NIC", DisplayName = "Standard 4-NIC",
            NicCount = 4, DefaultLoopbackPrefix = 32,
            NamingTemplate = "{building_code}-SRV{instance}",
        };
        Assert.Empty(ValidateProfile(p));
    }

    [Fact]
    public void Profile_MissingCodeAndName_ReportsBoth()
    {
        var errors = ValidateProfile(new ServerProfile
        {
            NicCount = 4, DefaultLoopbackPrefix = 32,
            NamingTemplate = "x",
        });
        Assert.Contains(errors, e => e.Contains("Code"));
        Assert.Contains(errors, e => e.Contains("Display name"));
    }

    [Fact]
    public void Profile_NicCountZero_Rejected()
    {
        var p = new ServerProfile
        {
            ProfileCode = "X", DisplayName = "X",
            NicCount = 0, DefaultLoopbackPrefix = 32,
            NamingTemplate = "x",
        };
        Assert.Contains(ValidateProfile(p), e => e.Contains("NIC count"));
    }

    [Fact]
    public void Profile_LoopbackPrefixOutOfRange_Rejected()
    {
        var p = new ServerProfile
        {
            ProfileCode = "X", DisplayName = "X",
            NicCount = 4, DefaultLoopbackPrefix = 129,   // v6 max is 128
            NamingTemplate = "x",
        };
        Assert.Contains(ValidateProfile(p), e => e.Contains("Loopback prefix"));
    }

    [Fact]
    public void Profile_EmptyNamingTemplate_Rejected()
    {
        var p = new ServerProfile
        {
            ProfileCode = "X", DisplayName = "X",
            NicCount = 4, DefaultLoopbackPrefix = 32,
            NamingTemplate = "",
        };
        Assert.Contains(ValidateProfile(p), e => e.Contains("Naming template"));
    }

    // ── Server ──────────────────────────────────────────────────

    [Fact]
    public void Server_New_RequiresBuildingAndProfile()
    {
        var s = new Server { Hostname = "MEP-91-SRV01" };
        // Defaults: BuildingId and ServerProfileId are null.
        var errors = ValidateServer(s, Mode.New);
        Assert.Contains(errors, e => e.Contains("Building"));
        Assert.Contains(errors, e => e.Contains("Server profile"));
    }

    [Fact]
    public void Server_NewWithFullContext_OK()
    {
        var s = new Server
        {
            Hostname = "MEP-91-SRV01",
            BuildingId = Guid.NewGuid(),
            ServerProfileId = Guid.NewGuid(),
        };
        Assert.Empty(ValidateServer(s, Mode.New));
    }

    [Fact]
    public void Server_EditDoesNotRequireParents()
    {
        // In Edit mode the parents are already set; the operator isn't
        // re-binding them. Blank FKs here would be a bug elsewhere,
        // not a user-facing validation error.
        var s = new Server { Hostname = "MEP-91-SRV01" };
        Assert.Empty(ValidateServer(s, Mode.Edit));
    }

    [Fact]
    public void Server_EmptyHostname_Rejected()
    {
        Assert.Contains(
            ValidateServer(new Server(), Mode.Edit),
            e => e.Contains("Hostname"));
    }

    // ── ServerNic ───────────────────────────────────────────────

    [Fact]
    public void Nic_New_RequiresServerId()
    {
        var n = new ServerNic { NicIndex = 0 };
        Assert.Contains(ValidateNic(n, Mode.New), e => e.Contains("Server"));
    }

    [Fact]
    public void Nic_NegativeIndex_Rejected()
    {
        // DB also CHECKs nic_index >= 0, but we surface the message
        // at the UI boundary so the user doesn't need a 500 to see it.
        Assert.Contains(
            ValidateNic(new ServerNic { NicIndex = -1, ServerId = Guid.NewGuid() }, Mode.New),
            e => e.Contains("NIC index"));
    }

    [Fact]
    public void Nic_EditDoesNotRequireServerId()
    {
        Assert.Empty(
            ValidateNic(new ServerNic { NicIndex = 0 }, Mode.Edit));
    }
}
