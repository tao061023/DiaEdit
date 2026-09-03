namespace DiaEditCore.Commands.Stations;

using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;

/// <summary>
/// 「新規登録（Create）」パターンのRail向け実装。CreateStationCommandと同じ設計。
///
/// EndpointA/EndpointB（接続トポロジー）は新規作成時点では未接続（NoneEndpointRef）で生成する。
/// 接続の確立はSwitcherコマンド実装時にあわせて設計する専用コマンドの責務とする。
/// ControlPointsも同様に空リストで生成し、形状編集は専用コマンドの責務とする。
///
/// ID採番はCreateStationCommandと同じ方針（セッション中は最大IdValue+1、欠番は詰めない。）
/// 
/// AffectedIdsは新規登録パターンの規約通り空集合。
/// </summary>
public sealed class CreateRailCommand : UndoableCommand<List<Rail>, Rail?>
{
    private readonly IdAllocator<RailId> _idAllocator;
    private readonly string _name;
    private readonly double _lengthM;
    private readonly double _speedLimitKph;
    private readonly RailRole _role;

    /// <summary>Execute()実行後、生成されたRailを呼び出し元が参照するためのプロパティ。</summary>
    public Rail? Created { get; private set; }

    public CreateRailCommand(
        List<Rail> rails,
        IdAllocator<RailId> idAllocator,
        string name,
        double lengthM,
        double speedLimitKph,
        RailRole role)
        : base(rails, new HashSet<ObjectId>()) // 新規登録：AffectedIdsは空集合
    {
        _idAllocator = idAllocator;
        _name = name;
        _lengthM = lengthM;
        _speedLimitKph = speedLimitKph;
        _role = role;
    }

    protected override IReadOnlySet<ObjectId> ComputeAffectedIdsAfterApply(List<Rail> target) =>
    Created is not null
        ? new HashSet<ObjectId> { new RailObjectId(Created.Id) }
        : new HashSet<ObjectId>();

    protected override Rail? CaptureSnapshot(List<Rail> target) => null;

    protected override void Apply(List<Rail> target)
    {
        if (Created is not null)
        {
            // Redo経路：初回Execute時に生成・保持したインスタンスを再挿入する。
            // AllocateNextIdは呼び直さない（§9.1項目23、CreateStationCommandと同じ理由）。
            target.Add(Created);
            return;
        }

        Created = new Rail
        {
            Id = _idAllocator.AllocateNext(),
            Name = _name,
            LengthM = _lengthM,
            SpeedLimitKph = _speedLimitKph,
            Role = _role,
            EndpointA = new NoneEndpointRef(),
            EndpointB = new NoneEndpointRef()
        };
        target.Add(Created);
    }

    protected override void Restore(List<Rail> target, Rail? snapshot)
    {
        if (Created is not null)
        {
            target.Remove(Created);
        }
    }
}