namespace DiaEditCore.Tests.Commands;

using DiaEditCore.ChangeNotification;
using DiaEditCore.Commands;
using DiaEditCore.Model;

using Xunit;

/// <summary>テスト用の単純な可変対象。実際のTrain等の代わりに使う。</summary>
public sealed class FakeTarget
{
    public int Value { get; set; }
}

/// <summary>FakeTarget.Valueを指定値に変更するテスト用コマンド（スナップショット方式の最小実装例）。</summary>
public sealed class SetValueCommand : UndoableCommand<FakeTarget, int>
{
    private readonly int _newValue;

    public SetValueCommand(FakeTarget target, int newValue, IReadOnlySet<ObjectId> affectedIds)
        : base(target, affectedIds)
    {
        _newValue = newValue;
    }

    protected override int CaptureSnapshot(FakeTarget target) => target.Value;
    protected override void Apply(FakeTarget target) => target.Value = _newValue;
    protected override void Restore(FakeTarget target, int snapshot) => target.Value = snapshot;
}

public sealed class RecordingObserver : ICacheChangeObserver
{
    public List<IReadOnlySet<ObjectId>> Received { get; } = new();
    public void OnChanged(IReadOnlySet<ObjectId> affectedIds) => Received.Add(affectedIds);
}

public class UndoableCommandTests
{
    private static IReadOnlySet<ObjectId> MakeAffectedIds() => new HashSet<ObjectId> { new RailObjectId(new RailId(1)) };

    [Fact]
    public void Executeで値が変更されAffectedIdsが返る()
    {
        var target = new FakeTarget { Value = 1 };
        var affectedIds = MakeAffectedIds();
        var command = new SetValueCommand(target, 99, affectedIds);

        var result = command.Execute();

        Assert.Equal(99, target.Value);
        Assert.Equal(affectedIds, result);
    }

    [Fact]
    public void Undoで実行前の値に戻る()
    {
        var target = new FakeTarget { Value = 1 };
        var command = new SetValueCommand(target, 99, MakeAffectedIds());
        command.Execute();

        command.Undo();

        Assert.Equal(1, target.Value);
    }

    [Fact]
    public void Execute前にUndoを呼ぶとInvalidOperationException()
    {
        var target = new FakeTarget { Value = 1 };
        var command = new SetValueCommand(target, 99, MakeAffectedIds());

        Assert.Throws<InvalidOperationException>(() => command.Undo());
    }
}

public class CommandInvokerTests
{
    private static IReadOnlySet<ObjectId> MakeAffectedIds() => new HashSet<ObjectId> { new RailObjectId(new RailId(1)) };

    [Fact]
    public void Executeで対象が変更されObserverに通知される()
    {
        var target = new FakeTarget { Value = 1 };
        var invoker = new CommandInvoker();
        var observer = new RecordingObserver();
        invoker.Subscribe(observer);

        invoker.Execute(new SetValueCommand(target, 99, MakeAffectedIds()));

        Assert.Equal(99, target.Value);
        Assert.Single(observer.Received);
    }

    [Fact]
    public void UndoでスタックのトップコマンドがUndoされCanRedoがtrueになる()
    {
        var target = new FakeTarget { Value = 1 };
        var invoker = new CommandInvoker();
        invoker.Execute(new SetValueCommand(target, 99, MakeAffectedIds()));

        invoker.Undo();

        Assert.Equal(1, target.Value);
        Assert.True(invoker.CanRedo);
        Assert.False(invoker.CanUndo);
    }

    [Fact]
    public void RedoでUndoした変更が再適用される()
    {
        var target = new FakeTarget { Value = 1 };
        var invoker = new CommandInvoker();
        invoker.Execute(new SetValueCommand(target, 99, MakeAffectedIds()));
        invoker.Undo();

        invoker.Redo();

        Assert.Equal(99, target.Value);
        Assert.True(invoker.CanUndo);
        Assert.False(invoker.CanRedo);
    }

    [Fact]
    public void Undoした後に新しいコマンドをExecuteするとRedo履歴が破棄される()
    {
        var target = new FakeTarget { Value = 1 };
        var invoker = new CommandInvoker();
        invoker.Execute(new SetValueCommand(target, 99, MakeAffectedIds()));
        invoker.Undo();

        invoker.Execute(new SetValueCommand(target, 42, MakeAffectedIds()));

        Assert.False(invoker.CanRedo);
        Assert.Equal(42, target.Value);
    }

    [Fact]
    public void Undoできない状態でUndoを呼んでも何も起きない()
    {
        var invoker = new CommandInvoker();

        invoker.Undo(); // 例外にならないこと

        Assert.False(invoker.CanUndo);
    }

    [Fact]
    public void UnsubscribeしたObserverには通知されない()
    {
        var target = new FakeTarget { Value = 1 };
        var invoker = new CommandInvoker();
        var observer = new RecordingObserver();
        invoker.Subscribe(observer);
        invoker.Unsubscribe(observer);

        invoker.Execute(new SetValueCommand(target, 99, MakeAffectedIds()));

        Assert.Empty(observer.Received);
    }

    [Fact]
    public void 複数Observerに同時に通知される()
    {
        var target = new FakeTarget { Value = 1 };
        var invoker = new CommandInvoker();
        var observer1 = new RecordingObserver();
        var observer2 = new RecordingObserver();
        invoker.Subscribe(observer1);
        invoker.Subscribe(observer2);

        invoker.Execute(new SetValueCommand(target, 99, MakeAffectedIds()));

        Assert.Single(observer1.Received);
        Assert.Single(observer2.Received);
    }
}
