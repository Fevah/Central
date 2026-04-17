using Central.Core.Services;

namespace Central.Tests.Services;

public class UndoServiceTests
{
    private class TestModel
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    [Fact]
    public void Initial_State_Empty()
    {
        var svc = new UndoService();
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Null(svc.UndoDescription);
        Assert.Null(svc.RedoDescription);
        Assert.Empty(svc.UndoHistory);
        Assert.Empty(svc.RedoHistory);
    }

    [Fact]
    public void RecordPropertyChange_AutoBatch_PushesToUndoStack()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "old" };

        svc.RecordPropertyChange(model, nameof(TestModel.Name), "old", "new");
        model.Name = "new";

        Assert.True(svc.CanUndo);
        Assert.Equal("Edit Name", svc.UndoDescription);
    }

    [Fact]
    public void Undo_RevertsSinglePropertyChange()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "old" };

        svc.RecordPropertyChange(model, nameof(TestModel.Name), "old", "new");
        model.Name = "new";

        svc.Undo();

        Assert.Equal("old", model.Name);
        Assert.False(svc.CanUndo);
        Assert.True(svc.CanRedo);
    }

    [Fact]
    public void Redo_ReappliesPropertyChange()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "old" };

        svc.RecordPropertyChange(model, nameof(TestModel.Name), "old", "new");
        model.Name = "new";

        svc.Undo();
        svc.Redo();

        Assert.Equal("new", model.Name);
        Assert.True(svc.CanUndo);
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void NewChange_ClearsRedoStack()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "v1" };

        svc.RecordPropertyChange(model, nameof(TestModel.Name), "v1", "v2");
        model.Name = "v2";

        svc.Undo(); // back to v1
        Assert.True(svc.CanRedo);

        // New change should clear redo
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "v1", "v3");
        model.Name = "v3";

        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void Batch_CommitsMultipleChanges()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "A", Value = 1 };

        svc.BeginBatch("Edit row");
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "A", "B");
        model.Name = "B";
        svc.RecordPropertyChange(model, nameof(TestModel.Value), 1, 2);
        model.Value = 2;
        svc.CommitBatch();

        Assert.True(svc.CanUndo);
        Assert.Equal("Edit row", svc.UndoDescription);

        // Single undo reverts both changes
        svc.Undo();
        Assert.Equal("A", model.Name);
        Assert.Equal(1, model.Value);
    }

    [Fact]
    public void Batch_MergesConsecutiveSamePropertyChanges()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "A" };

        svc.BeginBatch("Typing");
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "A", "AB");
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "AB", "ABC");
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "ABC", "ABCD");
        model.Name = "ABCD";
        svc.CommitBatch();

        // Should merge into a single change: A -> ABCD
        svc.Undo();
        Assert.Equal("A", model.Name);
    }

    [Fact]
    public void DiscardBatch_DoesNotCommit()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "A" };

        svc.BeginBatch("Discarded");
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "A", "B");
        svc.DiscardBatch();

        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void CommitBatch_EmptyBatch_DoesNotPush()
    {
        var svc = new UndoService();
        svc.BeginBatch("Empty");
        svc.CommitBatch();
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Clear_RemovesAllHistory()
    {
        var svc = new UndoService();
        var model = new TestModel { Name = "A" };

        svc.RecordPropertyChange(model, nameof(TestModel.Name), "A", "B");
        model.Name = "B";
        svc.RecordPropertyChange(model, nameof(TestModel.Name), "B", "C");
        model.Name = "C";
        svc.Undo(); // creates redo entry

        Assert.True(svc.CanUndo);
        Assert.True(svc.CanRedo);

        svc.Clear();

        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Empty(svc.UndoHistory);
        Assert.Empty(svc.RedoHistory);
    }

    [Fact]
    public void RecordAdd_UndoRemovesItem()
    {
        var svc = new UndoService();
        var list = new List<string> { "A", "B" };

        list.Add("C");
        svc.RecordAdd(list, "C", "Add item");

        svc.Undo();
        Assert.Equal(2, list.Count);
        Assert.DoesNotContain("C", list);
    }

    [Fact]
    public void RecordAdd_RedoReAddsItem()
    {
        var svc = new UndoService();
        var list = new List<string> { "A" };

        list.Add("B");
        svc.RecordAdd(list, "B", "Add B");

        svc.Undo();
        Assert.Single(list);

        svc.Redo();
        Assert.Equal(2, list.Count);
        Assert.Contains("B", list);
    }

    [Fact]
    public void RecordRemove_UndoReinsertsItem()
    {
        var svc = new UndoService();
        var list = new List<string> { "A", "B", "C" };

        svc.RecordRemove(list, "B", 1, "Delete item");
        list.RemoveAt(1);

        svc.Undo();
        Assert.Equal(3, list.Count);
        Assert.Equal("B", list[1]);
    }

    [Fact]
    public void RecordRemove_RedoRemovesAgain()
    {
        var svc = new UndoService();
        var list = new List<string> { "X", "Y", "Z" };

        svc.RecordRemove(list, "Y", 1, "Remove Y");
        list.RemoveAt(1);

        svc.Undo();
        svc.Redo();

        Assert.Equal(2, list.Count);
        Assert.DoesNotContain("Y", list);
    }

    [Fact]
    public void StateChanged_FiresOnUndoPush()
    {
        var svc = new UndoService();
        int fireCount = 0;
        svc.StateChanged += (_, _) => fireCount++;

        svc.RecordPropertyChange(new TestModel(), "Name", "a", "b");
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void StateChanged_FiresOnUndo()
    {
        var svc = new UndoService();
        svc.RecordPropertyChange(new TestModel(), "Name", "a", "b");

        int fireCount = 0;
        svc.StateChanged += (_, _) => fireCount++;

        svc.Undo();
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void StateChanged_FiresOnRedo()
    {
        var svc = new UndoService();
        svc.RecordPropertyChange(new TestModel(), "Name", "a", "b");
        svc.Undo();

        int fireCount = 0;
        svc.StateChanged += (_, _) => fireCount++;

        svc.Redo();
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void StateChanged_FiresOnClear()
    {
        var svc = new UndoService();
        int fireCount = 0;
        svc.StateChanged += (_, _) => fireCount++;

        svc.Clear();
        Assert.Equal(1, fireCount);
    }

    [Fact]
    public void UndoHistory_ReturnsDescriptionsInOrder()
    {
        var svc = new UndoService();
        svc.RecordPropertyChange(new TestModel(), "Name", "a", "b");
        svc.RecordPropertyChange(new TestModel(), "Value", 1, 2);

        var history = svc.UndoHistory;
        Assert.Equal(2, history.Count);
        // Stack order: most recent first
        Assert.Equal("Edit Value", history[0]);
        Assert.Equal("Edit Name", history[1]);
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNothing()
    {
        var svc = new UndoService();
        svc.Undo(); // should not throw
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Redo_WhenEmpty_DoesNothing()
    {
        var svc = new UndoService();
        svc.Redo(); // should not throw
        Assert.False(svc.CanRedo);
    }

    [Fact]
    public void MultipleUndoRedo_WorksCorrectly()
    {
        var svc = new UndoService();
        var model = new TestModel { Value = 0 };

        svc.RecordPropertyChange(model, nameof(TestModel.Value), 0, 1);
        model.Value = 1;
        svc.RecordPropertyChange(model, nameof(TestModel.Value), 1, 2);
        model.Value = 2;
        svc.RecordPropertyChange(model, nameof(TestModel.Value), 2, 3);
        model.Value = 3;

        svc.Undo(); // 3 -> 2
        Assert.Equal(2, model.Value);
        svc.Undo(); // 2 -> 1
        Assert.Equal(1, model.Value);
        svc.Redo(); // 1 -> 2
        Assert.Equal(2, model.Value);
        svc.Redo(); // 2 -> 3
        Assert.Equal(3, model.Value);
    }
}
