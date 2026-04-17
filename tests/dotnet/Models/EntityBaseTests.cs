using Central.Core.Models;

namespace Central.Tests.Models;

public class EntityBaseTests
{
    private class TestEntity : EntityBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }

        private int _count;
        public int Count { get => _count; set => SetField(ref _count, value); }
    }

    [Fact]
    public void SetField_RaisesPropertyChanged_WhenValueDiffers()
    {
        var entity = new TestEntity();
        var changed = new List<string>();
        entity.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        entity.Name = "Test";
        Assert.Contains("Name", changed);
    }

    [Fact]
    public void SetField_DoesNotRaise_WhenValueSame()
    {
        var entity = new TestEntity { Name = "Test" };
        var changed = new List<string>();
        entity.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        entity.Name = "Test"; // same value
        Assert.DoesNotContain("Name", changed);
    }

    [Fact]
    public void SetField_ChangesValue_WhenDifferent()
    {
        var entity = new TestEntity();

        // We test indirectly via the Name property which calls SetField
        entity.Name = "New";
        Assert.Equal("New", entity.Name);
    }

    [Fact]
    public void TakeSnapshot_CapturesAllProperties()
    {
        var entity = new TestEntity
        {
            Id = 42,
            Name = "Widget",
            Count = 5,
            CreatedAt = new DateTime(2026, 1, 1),
            IsDeleted = false
        };

        var snap = entity.TakeSnapshot();

        Assert.Equal(42, snap["Id"]);
        Assert.Equal("Widget", snap["Name"]);
        Assert.Equal(5, snap["Count"]);
        Assert.Equal(new DateTime(2026, 1, 1), snap["CreatedAt"]);
        Assert.Equal(false, snap["IsDeleted"]);
    }

    [Fact]
    public void TakeSnapshot_IncludesNullValues()
    {
        var entity = new TestEntity();
        var snap = entity.TakeSnapshot();
        Assert.True(snap.ContainsKey("DeletedAt"));
        Assert.Null(snap["DeletedAt"]);
    }

    [Fact]
    public void SoftDelete_Properties()
    {
        var entity = new TestEntity();
        var changed = new List<string>();
        entity.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        entity.IsDeleted = true;
        entity.DeletedAt = new DateTime(2026, 3, 30);

        Assert.True(entity.IsDeleted);
        Assert.Equal(new DateTime(2026, 3, 30), entity.DeletedAt);
        Assert.Contains("IsDeleted", changed);
        Assert.Contains("DeletedAt", changed);
    }

    [Fact]
    public void Id_PropertyChanged()
    {
        var entity = new TestEntity();
        var changed = new List<string>();
        entity.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        entity.Id = 99;
        Assert.Contains("Id", changed);
        Assert.Equal(99, entity.Id);
    }

    [Fact]
    public void UpdatedAt_PropertyChanged()
    {
        var entity = new TestEntity();
        var changed = new List<string>();
        entity.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var now = DateTime.UtcNow;
        entity.UpdatedAt = now;
        Assert.Contains("UpdatedAt", changed);
        Assert.Equal(now, entity.UpdatedAt);
    }
}
