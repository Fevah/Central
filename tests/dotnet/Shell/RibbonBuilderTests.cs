using Central.Core.Shell;

namespace Central.Tests.Shell;

public class RibbonBuilderTests
{
    [Fact]
    public void AddPage_CreatesPage()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New", null, null, () => { });
            });
        });

        Assert.Single(builder.Pages);
        Assert.Equal("Home", builder.Pages[0].Header);
        Assert.Equal(1, builder.Pages[0].SortOrder);
    }

    [Fact]
    public void AddPage_WithPermission()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Admin", 10, "admin:view", page =>
        {
            page.AddGroup("Users", group =>
            {
                group.AddButton("New User", "admin:edit", null, () => { });
            });
        });

        Assert.Equal("admin:view", builder.Pages[0].RequiredPermission);
    }

    [Fact]
    public void AddGroup_CreatesGroup()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Actions", group => { });
            page.AddGroup("View", group => { });
        });

        Assert.Equal(2, builder.Pages[0].Groups.Count);
        Assert.Equal("Actions", builder.Pages[0].Groups[0].Header);
        Assert.Equal("View", builder.Pages[0].Groups[1].Header);
    }

    [Fact]
    public void AddButton_CreatesButton()
    {
        var builder = new RibbonBuilder();
        bool clicked = false;

        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New", "ipam:edit", "icon.svg", () => clicked = true);
            });
        });

        var btn = builder.Pages[0].Groups[0].Items[0] as RibbonButtonRegistration;
        Assert.NotNull(btn);
        Assert.Equal("New", btn!.Content);
        Assert.Equal("ipam:edit", btn.Permission);
        Assert.Equal("icon.svg", btn.Glyph);
        Assert.False(btn.IsLarge);

        btn.OnClick();
        Assert.True(clicked);
    }

    [Fact]
    public void AddLargeButton_CreatesLargeButton()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddLargeButton("Delete", "ipam:delete", "large-icon.svg", "Delete device", () => { });
            });
        });

        var btn = builder.Pages[0].Groups[0].Items[0] as RibbonButtonRegistration;
        Assert.NotNull(btn);
        Assert.True(btn!.IsLarge);
        Assert.Equal("large-icon.svg", btn.LargeGlyph);
        Assert.Equal("Delete device", btn.ToolTip);
    }

    [Fact]
    public void AddCheckButton_CreatesCheckButton()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("IPAM", "IpamPanel");
            });
        });

        var chk = builder.Pages[0].Groups[0].Items[0] as RibbonCheckButtonRegistration;
        Assert.NotNull(chk);
        Assert.Equal("IPAM", chk!.Content);
        Assert.Equal("IpamPanel", chk.PanelId);
    }

    [Fact]
    public void AddToggleButton_CreatesToggle()
    {
        var builder = new RibbonBuilder();
        bool toggleState = false;

        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("View", group =>
            {
                group.AddToggleButton("Auto Refresh", null, isOn => toggleState = isOn);
            });
        });

        var toggle = builder.Pages[0].Groups[0].Items[0] as RibbonToggleRegistration;
        Assert.NotNull(toggle);
        Assert.Equal("Auto Refresh", toggle!.Content);

        toggle.OnToggle(true);
        Assert.True(toggleState);
    }

    [Fact]
    public void AddSeparator_CreatesSeparator()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New", null, null, () => { });
                group.AddSeparator();
                group.AddButton("Delete", null, null, () => { });
            });
        });

        Assert.Equal(3, builder.Pages[0].Groups[0].Items.Count);
        Assert.IsType<RibbonSeparatorRegistration>(builder.Pages[0].Groups[0].Items[1]);
    }

    [Fact]
    public void AddSplitButton_CreatesWithSubItems()
    {
        var builder = new RibbonBuilder();
        bool primaryClicked = false;

        builder.AddPage("Home", 1, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddSplitButton("Export", null, "export.svg", () => primaryClicked = true, subItems =>
                {
                    subItems.Add(new RibbonButtonRegistration { Content = "CSV", OnClick = () => { } });
                    subItems.Add(new RibbonButtonRegistration { Content = "Excel", OnClick = () => { } });
                });
            });
        });

        var split = builder.Pages[0].Groups[0].Items[0] as RibbonSplitButtonRegistration;
        Assert.NotNull(split);
        Assert.Equal("Export", split!.Content);
        Assert.Equal(2, split.SubItems.Count);
        Assert.Equal("CSV", split.SubItems[0].Content);

        split.OnClick!();
        Assert.True(primaryClicked);
    }

    [Fact]
    public void RibbonGroupRegistration_Accessors()
    {
        var group = new RibbonGroupRegistration { Header = "Test" };
        group.Items.Add(new RibbonButtonRegistration { Content = "Btn" });
        group.Items.Add(new RibbonCheckButtonRegistration { Content = "Chk" });
        group.Items.Add(new RibbonToggleRegistration { Content = "Tog" });
        group.Items.Add(new RibbonSplitButtonRegistration { Content = "Split" });
        group.Items.Add(new RibbonSeparatorRegistration());

        Assert.Single(group.Buttons);
        Assert.Single(group.CheckButtons);
        Assert.Single(group.ToggleButtons);
        Assert.Single(group.SplitButtons);
    }

    [Fact]
    public void MultiplePages_SortOrder()
    {
        var builder = new RibbonBuilder();
        builder.AddPage("Admin", 10, page => { page.AddGroup("G", g => g.AddButton("B", null, null, () => { })); });
        builder.AddPage("Home", 1, page => { page.AddGroup("G", g => g.AddButton("B", null, null, () => { })); });
        builder.AddPage("Devices", 3, page => { page.AddGroup("G", g => g.AddButton("B", null, null, () => { })); });

        Assert.Equal(3, builder.Pages.Count);
        // Pages are stored in insertion order, not sorted
        Assert.Equal("Admin", builder.Pages[0].Header);
        Assert.Equal("Home", builder.Pages[1].Header);
    }
}
