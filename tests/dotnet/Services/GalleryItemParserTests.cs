using Central.Core.Services;

namespace Central.Tests.Services;

public class GalleryItemParserTests
{
    [Fact]
    public void Parse_Null_ReturnsEmpty()
    {
        Assert.Empty(GalleryItemParser.Parse(null));
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(GalleryItemParser.Parse(""));
    }

    [Fact]
    public void Parse_Whitespace_ReturnsEmpty()
    {
        Assert.Empty(GalleryItemParser.Parse("   "));
    }

    [Fact]
    public void Parse_CaptionOnly_NoIcon()
    {
        var tiles = GalleryItemParser.Parse("Save");
        var tile = Assert.Single(tiles);
        Assert.Equal("Save", tile.Caption);
        Assert.Null(tile.IconName);
    }

    [Fact]
    public void Parse_CaptionWithIcon_Pair()
    {
        var tiles = GalleryItemParser.Parse("Save|Save16");
        var tile = Assert.Single(tiles);
        Assert.Equal("Save", tile.Caption);
        Assert.Equal("Save16", tile.IconName);
    }

    [Fact]
    public void Parse_MultipleEntries_SplitsOnSemicolon()
    {
        var tiles = GalleryItemParser.Parse("Save|SaveIcon;Delete|DeleteIcon;Refresh");
        Assert.Equal(3, tiles.Count);
        Assert.Equal("Save", tiles[0].Caption);
        Assert.Equal("SaveIcon", tiles[0].IconName);
        Assert.Equal("Delete", tiles[1].Caption);
        Assert.Equal("DeleteIcon", tiles[1].IconName);
        Assert.Equal("Refresh", tiles[2].Caption);
        Assert.Null(tiles[2].IconName);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAroundSeparators()
    {
        var tiles = GalleryItemParser.Parse("  Save  |  SaveIcon  ;  Delete  ");
        Assert.Equal(2, tiles.Count);
        Assert.Equal("Save", tiles[0].Caption);
        Assert.Equal("SaveIcon", tiles[0].IconName);
        Assert.Equal("Delete", tiles[1].Caption);
    }

    [Fact]
    public void Parse_SkipsEmptyEntries()
    {
        var tiles = GalleryItemParser.Parse("Save;;;Delete;");
        Assert.Equal(2, tiles.Count);
        Assert.Equal("Save", tiles[0].Caption);
        Assert.Equal("Delete", tiles[1].Caption);
    }

    [Fact]
    public void Parse_EmptyIconAfterPipe_YieldsNullIcon()
    {
        var tiles = GalleryItemParser.Parse("Save|");
        var tile = Assert.Single(tiles);
        Assert.Equal("Save", tile.Caption);
        Assert.Null(tile.IconName);
    }

    [Fact]
    public void Parse_IconWithoutCaption_KeepsEmptyCaption()
    {
        var tiles = GalleryItemParser.Parse("|OnlyIcon");
        var tile = Assert.Single(tiles);
        Assert.Equal("", tile.Caption);
        Assert.Equal("OnlyIcon", tile.IconName);
    }
}
