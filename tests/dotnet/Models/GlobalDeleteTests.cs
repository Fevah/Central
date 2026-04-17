using Central.Engine.Models;

namespace Central.Tests.Models;

/// <summary>
/// Tests for the IsUnsavedRow pattern — verifies that every model type
/// correctly identifies new (unsaved) rows by their default/zero Id.
/// This mirrors the GlobalDelete_ItemClick logic in MainWindow.
/// </summary>
public class GlobalDeleteTests
{
    // ── Helper that mirrors MainWindow.IsUnsavedRow ──

    private static bool IsUnsavedRow(object row) => row switch
    {
        DeviceRecord d  => string.IsNullOrEmpty(d.Id) || d.Id == "0",
        P2PLink p       => p.Id == 0,
        B2BLink b       => b.Id == 0,
        FWLink f        => f.Id == 0,
        VlanEntry v     => v.Id == 0,
        MlagConfig m    => m.Id == 0,
        MstpConfig t    => t.Id == 0,
        ServerAS sa     => sa.Id == 0,
        IpRange ir      => ir.Id == 0,
        Server sv       => sv.Id == 0,
        AsnDefinition a => a.Id == 0,
        LookupItem li   => li.Id == 0,
        AppUser u       => u.Id == 0,
        TaskItem ti     => ti.Id == 0,
        SwitchRecord sw => sw.Id == Guid.Empty,
        BgpRecord bg    => bg.Id == 0,
        RoleRecord rr   => rr.Id == 0,
        SdRequest sdr   => sdr.Id == 0,
        SdGroup sdg     => sdg.Id == 0,
        SdTechnician sdt => sdt.Id == 0,
        SdRequester sdq  => sdq.Id == 0,
        _ => false
    };

    // ── New instances are unsaved ──

    [Fact] public void DeviceRecord_NewIsUnsaved() => Assert.True(IsUnsavedRow(new DeviceRecord()));
    [Fact] public void P2PLink_NewIsUnsaved() => Assert.True(IsUnsavedRow(new P2PLink()));
    [Fact] public void B2BLink_NewIsUnsaved() => Assert.True(IsUnsavedRow(new B2BLink()));
    [Fact] public void FWLink_NewIsUnsaved() => Assert.True(IsUnsavedRow(new FWLink()));
    [Fact] public void VlanEntry_NewIsUnsaved() => Assert.True(IsUnsavedRow(new VlanEntry()));
    [Fact] public void MlagConfig_NewIsUnsaved() => Assert.True(IsUnsavedRow(new MlagConfig()));
    [Fact] public void MstpConfig_NewIsUnsaved() => Assert.True(IsUnsavedRow(new MstpConfig()));
    [Fact] public void ServerAS_NewIsUnsaved() => Assert.True(IsUnsavedRow(new ServerAS()));
    [Fact] public void IpRange_NewIsUnsaved() => Assert.True(IsUnsavedRow(new IpRange()));
    [Fact] public void Server_NewIsUnsaved() => Assert.True(IsUnsavedRow(new Server()));
    [Fact] public void AsnDefinition_NewIsUnsaved() => Assert.True(IsUnsavedRow(new AsnDefinition()));
    [Fact] public void LookupItem_NewIsUnsaved() => Assert.True(IsUnsavedRow(new LookupItem()));
    [Fact] public void AppUser_NewIsUnsaved() => Assert.True(IsUnsavedRow(new AppUser()));
    [Fact] public void TaskItem_NewIsUnsaved() => Assert.True(IsUnsavedRow(new TaskItem()));
    [Fact] public void SwitchRecord_NewIsUnsaved() => Assert.True(IsUnsavedRow(new SwitchRecord()));
    [Fact] public void BgpRecord_NewIsUnsaved() => Assert.True(IsUnsavedRow(new BgpRecord()));
    [Fact] public void RoleRecord_NewIsUnsaved() => Assert.True(IsUnsavedRow(new RoleRecord()));
    [Fact] public void SdRequest_NewIsUnsaved() => Assert.True(IsUnsavedRow(new SdRequest()));
    [Fact] public void SdGroup_NewIsUnsaved() => Assert.True(IsUnsavedRow(new SdGroup()));
    [Fact] public void SdTechnician_NewIsUnsaved() => Assert.True(IsUnsavedRow(new SdTechnician()));
    [Fact] public void SdRequester_NewIsUnsaved() => Assert.True(IsUnsavedRow(new SdRequester()));

    // ── Saved instances are NOT unsaved ──

    [Fact] public void DeviceRecord_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new DeviceRecord { Id = "42" }));
    [Fact] public void P2PLink_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new P2PLink { Id = 1 }));
    [Fact] public void B2BLink_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new B2BLink { Id = 1 }));
    [Fact] public void FWLink_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new FWLink { Id = 1 }));
    [Fact] public void TaskItem_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new TaskItem { Id = 99 }));
    [Fact] public void SwitchRecord_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new SwitchRecord { Id = Guid.NewGuid() }));
    [Fact] public void BgpRecord_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new BgpRecord { Id = 5 }));
    [Fact] public void RoleRecord_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new RoleRecord { Id = 1 }));
    [Fact] public void SdRequest_SavedIsNotUnsaved() => Assert.False(IsUnsavedRow(new SdRequest { Id = 100 }));

    // ── Edge cases ──

    [Fact]
    public void DeviceRecord_Id0IsUnsaved()
    {
        Assert.True(IsUnsavedRow(new DeviceRecord { Id = "0" }));
    }

    [Fact]
    public void UnknownType_ReturnsFalse()
    {
        Assert.False(IsUnsavedRow("not a model"));
    }
}
