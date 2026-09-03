namespace DiaEditCore.Commands;

using DiaEditCore.Model;

/// <summary>
/// 5.10.1節「(c) TransactionCommand」の実装。複数のUndoableCommandを1つの操作として束ねる。
///
/// 設計方針（v12.14）：
///   - コンストラクタはIUndoableCommandそのものではなくFunc&lt;IUndoableCommand&gt;（遅延評価ファクトリ）の
///     リストを受け取る。理由：Station作成時の空FloorUnit同時生成（4.2節）のように、後続コマンドが
///     先行コマンドの実行結果（例：CreateStationCommand.Created.Idとして採番されるStationId）に
///     依存するケースがあり、コンストラクタ時点では後続コマンドを構築できないため。
///     ファクトリはExecute()内で、直前までのコマンドが実際にExecute()された後に順次呼び出される。
///   - Undo()は実行済みコマンドを逆順にUndo()する（後から作られた依存が先に取り消される）。
///   - AffectedIdsは実行時に確定するため、UndoableCommand&lt;TTarget,TSnapshot&gt;基底は使わず
///     IUndoableCommandを直接実装する。
/// </summary>
public sealed class TransactionCommand : IUndoableCommand
{
    private readonly IReadOnlyList<Func<IUndoableCommand>> _factories;
    private readonly List<IUndoableCommand> _executed = new();
    private bool _executedOnce;

    public TransactionCommand(IReadOnlyList<Func<IUndoableCommand>> factories)
    {
        if (factories.Count == 0)
            throw new ArgumentException("TransactionCommand: factoriesは1件以上必要です", nameof(factories));

        _factories = factories;
    }

    public IReadOnlySet<ObjectId> Execute()
    {
        var affected = new HashSet<ObjectId>();

        if (_executedOnce)
        {
            // Redo経路：ファクトリは呼び直さず、Execute時に生成済みの
            // コマンドインスタンスへ再度Execute()する（各コマンド自身のRedo安全な
            // Apply()実装、§9.1項目23が前提として効く）。
            foreach (var command in _executed)
            {
                affected.UnionWith(command.Execute());
            }
            return affected;
        }

        foreach (var factory in _factories)
        {
            var command = factory();
            affected.UnionWith(command.Execute());
            _executed.Add(command);
        }

        _executedOnce = true;
        return affected;
    }

    public IReadOnlySet<ObjectId> Undo()
    {
        if (!_executedOnce)
            throw new InvalidOperationException($"{nameof(TransactionCommand)}: Execute()より前にUndo()が呼ばれた");

        var affected = new HashSet<ObjectId>();
        for (var i = _executed.Count - 1; i >= 0; i--)
        {
            affected.UnionWith(_executed[i].Undo());
        }

        return affected;
    }
}