namespace DiaEditApp.ViewModels.Stations;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DiaEditApp.ViewModels; // IAffectedByObjectId, ChangeNotificationBridge

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// UI設計書§4.2.3「構内配線図ポップアップ」の暫定代替（キャンバス未実装）。
/// FloorUnit詳細画面＝Rail（線路）管理画面と位置づける（Tao様確認済み、v13.7セッション）。
/// FloorUnit自身のName編集は本画面の責務外（StationDetailViewModel側で行う）。
///
/// §9.2項目29のテンプレート（StationDetailViewModelで確立したBuildEditedSnapshot／
/// CaptureCurrentSnapshot／IsDirty／差分判定Saveパターン）を、Rail属性編集（選択中Rail）へ適用する。
///
/// Railは自身のFloorUnitIdを持たない（§4.4.3、EndpointA/Bの接続先端点オブジェクト経由で導出される
/// 派生関係）ため、「このFloorUnitに属するRail」は都度Rails全体をフィルタして導出する
/// （専用逆引きIndexは今回新設しない。DeleteRailCommandの3経路チェックと同じ判断基準：
/// 消費者がこのViewModelのみで件数規模も小さいため線形走査で足りる）。
///
/// ObservedIdsはStationDetailViewModelと同じ設計：_session.Current側の生きたコレクションを
/// 都度再評価する。Create系コマンドはComputeAffectedIdsAfterApplyで新規オブジェクト自身の
/// ObjectIdのみをAffectedIdsとするが、Apply()は既にNotifyより前に完了しているため、
/// ObservedIdsの再評価時点では新規Rail（＋アタッチ済みの端点）が既にセッション側の
/// コレクションに反映済みであり、結果的に自動的に拾える（親IDを明示的に含める工夫は不要）。
/// </summary>
public sealed partial class FloorUnitDetailViewModel : ViewModelBase, IAffectedByObjectId, IDisposable
{
    public IReadOnlyList<RailRole> RailRoles { get; } = Enum.GetValues<RailRole>();
    public IReadOnlyList<RailEndpointKind> EndpointKinds { get; } = Enum.GetValues<RailEndpointKind>();
    public IReadOnlyList<EntryPointType> EntryPointTypes { get; } = Enum.GetValues<EntryPointType>();

    private readonly FloorUnit _floorUnit;
    private readonly ProjectSession _session;
    private readonly CommandInvoker _invoker;
    private readonly ChangeNotificationBridge _bridge;
    private readonly Action _goBack;

    public string FloorUnitName => _floorUnit.Name;

    public ObservableCollection<Rail> Rails { get; } = new();

    [ObservableProperty]
    public partial Rail? SelectedRail { get; set; }

    // ---- 新規Rail入力欄 ----

    [ObservableProperty]
    public partial string NewRailName { get; set; } = "";

    [ObservableProperty]
    public partial double NewRailLengthM { get; set; }

    [ObservableProperty]
    public partial double NewRailSpeedLimitKph { get; set; }

    [ObservableProperty]
    public partial RailRole NewRailRole { get; set; } = RailRole.Normal;

    [ObservableProperty]
    public partial RailEndpointKind NewEndpointAKind { get; set; } = RailEndpointKind.None;
    [ObservableProperty]
    public partial int NewEndpointAX { get; set; }
    [ObservableProperty]
    public partial int NewEndpointAY { get; set; }
    [ObservableProperty]
    public partial EntryPointType NewEndpointAEntryType { get; set; } = EntryPointType.Both;

    [ObservableProperty]
    public partial RailEndpointKind NewEndpointBKind { get; set; } = RailEndpointKind.None;
    [ObservableProperty]
    public partial int NewEndpointBX { get; set; }
    [ObservableProperty]
    public partial int NewEndpointBY { get; set; }
    [ObservableProperty]
    public partial EntryPointType NewEndpointBEntryType { get; set; } = EntryPointType.Both;

    // ---- 選択中Rail属性編集欄（§9.2項目29テンプレート） ----

    [ObservableProperty]
    public partial string EditName { get; set; } = "";
    [ObservableProperty]
    public partial double EditLengthM { get; set; }
    [ObservableProperty]
    public partial double EditSpeedLimitKph { get; set; }
    [ObservableProperty]
    public partial RailRole EditRole { get; set; } = RailRole.Normal;

    partial void OnEditNameChanged(string value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnEditLengthMChanged(double value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnEditSpeedLimitKphChanged(double value) => OnPropertyChanged(nameof(IsDirty));
    partial void OnEditRoleChanged(RailRole value) => OnPropertyChanged(nameof(IsDirty));

    /// <summary>選択中Railが無い間はIsDirty=falseとし、「決定」ボタンを無効化する。</summary>
    public bool IsDirty => SelectedRail is not null && !BuildEditedSnapshot().Equals(CaptureCurrentSnapshot(SelectedRail));

    [ObservableProperty]
    public partial string? DeleteRailError { get; set; }
    public bool HasDeleteRailError => !string.IsNullOrEmpty(DeleteRailError);
    partial void OnDeleteRailErrorChanged(string? value) => OnPropertyChanged(nameof(HasDeleteRailError));

    /// <summary>
    /// StationDetailViewModel.ObservedIdsと同じ設計。FloorUnit自身に加え、現在このFloorUnitに
    /// 属する（端点経由で導出される）Rail群のIdを都度算出する。
    /// </summary>
    public IReadOnlySet<ObjectId> ObservedIds
    {
        get
        {
            var ids = new HashSet<ObjectId> { new FloorUnitObjectId(_floorUnit.Id) };
            foreach (var rail in RailsBelongingToThisFloorUnit())
                ids.Add(new RailObjectId(rail.Id));
            return ids;
        }
    }

    public FloorUnitDetailViewModel(
        FloorUnit floorUnit, ProjectSession session, CommandInvoker invoker,
        ChangeNotificationBridge bridge, Action goBack)
    {
        _floorUnit = floorUnit;
        _session = session;
        _invoker = invoker;
        _bridge = bridge;
        _goBack = goBack;
        _bridge.Subscribe(this);

        ReloadRails();
    }

    /// <summary>
    /// endpointの参照先オブジェクトのFloorUnitIdを解決する。NoneEndpointRef・参照先未検出はnull。
    /// Switcherも解決対象に含める（既存Switcherへの接続はRail作成時の対象外だが、将来の横展開・
    /// 表示目的のため解決自体はしておく）。
    /// </summary>
    private FloorUnitId? ResolveFloorUnitId(RailEndpointRef endpoint) => endpoint switch
    {
        NoneEndpointRef n => _session.Current.NoneEndpoints.FirstOrDefault(x => x.Id == n.Id)?.Base.FloorUnitId,
        BoundaryPointEndpointRef b => _session.Current.BoundaryPoints.FirstOrDefault(x => x.Id == b.Id)?.Base.FloorUnitId,
        EntryPointEndpointRef e => _session.Current.EntryPoints.FirstOrDefault(x => x.Id == e.Id)?.Base.FloorUnitId,
        BufferStopEndpointRef bs => _session.Current.BufferStops.FirstOrDefault(x => x.Id == bs.Id)?.Base.FloorUnitId,
        SwitcherEndpointRef sw => _session.Current.Switchers.FirstOrDefault(x => x.Id == sw.Id)?.Base.FloorUnitId,
        _ => null,
    };

    private IEnumerable<Rail> RailsBelongingToThisFloorUnit() =>
        _session.Current.Rails.Where(r =>
            ResolveFloorUnitId(r.EndpointA) == _floorUnit.Id ||
            ResolveFloorUnitId(r.EndpointB) == _floorUnit.Id);

    private void ReloadRails()
    {
        Rails.Clear();
        foreach (var rail in RailsBelongingToThisFloorUnit())
            Rails.Add(rail);
    }

    void IAffectedByObjectId.OnAffected()
    {
        if (!_session.Current.FloorUnits.Contains(_floorUnit))
        {
            _goBack();
            return;
        }

        ReloadRails();

        // 選択中Railが削除されていた場合は選択解除し、編集欄をクリアする。
        if (SelectedRail is not null && !Rails.Contains(SelectedRail))
        {
            SelectedRail = null;
        }
    }

    partial void OnSelectedRailChanged(Rail? value)
    {
        if (value is null)
        {
            EditName = "";
            EditLengthM = 0;
            EditSpeedLimitKph = 0;
            EditRole = RailRole.Normal;
        }
        else
        {
            EditName = value.Name;
            EditLengthM = value.LengthM;
            EditSpeedLimitKph = value.SpeedLimitKph;
            EditRole = value.Role;
        }
        OnPropertyChanged(nameof(IsDirty));
    }

    private RailSnapshot BuildEditedSnapshot() => new(EditName, EditLengthM, EditSpeedLimitKph, EditRole);

    private static RailSnapshot CaptureCurrentSnapshot(Rail rail) => new(rail.Name, rail.LengthM, rail.SpeedLimitKph, rail.Role);

    /// <summary>選択中Railへの属性変更を確定する（§9.2項目31と同じ差分判定：無変更ならコマンド発行しない）。</summary>
    [RelayCommand]
    private void SaveSelectedRail()
    {
        if (SelectedRail is null) return;

        var newValues = BuildEditedSnapshot();
        if (newValues.Equals(CaptureCurrentSnapshot(SelectedRail)))
            return;

        var command = new ChangeRailAttributesCommand(SelectedRail, newValues, _session);
        _invoker.Execute(command); // OnAffected経由でReloadRails()される
    }

    /// <summary>
    /// RailEndpointKind＋座標＋（EntryPointの場合のみ）EntryPointTypeから、
    /// RailCreationWorkflowへ渡すRailEndpointCreationSpecを組み立てる。
    /// </summary>
    private RailEndpointCreationSpec BuildEndpointSpec(RailEndpointKind kind, int x, int y, EntryPointType entryType) => kind switch
    {
        RailEndpointKind.None => new NoneEndpointCreationSpec(_floorUnit.Id, new Point(x, y)),
        RailEndpointKind.BoundaryPoint => new BoundaryPointCreationSpec(_floorUnit.Id, new Point(x, y)),
        RailEndpointKind.EntryPoint => new EntryPointCreationSpec(_floorUnit.Id, new Point(x, y), entryType),
        RailEndpointKind.BufferStop => new BufferStopCreationSpec(_floorUnit.Id, new Point(x, y)),
        _ => throw new NotSupportedException($"未知のRailEndpointKind: {kind}"),
    };

    /// <summary>
    /// UI設計書§7.1「駅構内オブジェクト新規配置」：Rail作成＝両端点オブジェクトの作成と等価
    /// （Tao様確認済み）。RailCreationWorkflowで1つのTransactionCommandとして実行する。
    /// Switcherは選択肢に含まない（別導線、EndpointKindsにも列挙しない）。
    /// </summary>
    [RelayCommand]
    private void AddRail()
    {
        var endpointA = BuildEndpointSpec(NewEndpointAKind, NewEndpointAX, NewEndpointAY, NewEndpointAEntryType);
        var endpointB = BuildEndpointSpec(NewEndpointBKind, NewEndpointBX, NewEndpointBY, NewEndpointBEntryType);

        var command = RailCreationWorkflow.CreateRailWithEndpoints(
            _session.Current.Rails, _session.RailIds,
            NewRailName, NewRailLengthM, NewRailSpeedLimitKph, NewRailRole,
            endpointA, endpointB,
            _session.Current.NoneEndpoints, _session.NoneEndpointIds,
            _session.Current.BoundaryPoints, _session.BoundaryPointIds,
            _session.Current.EntryPoints, _session.EntryPointIds,
            _session.Current.BufferStops, _session.BufferStopIds,
            _session);

        _invoker.Execute(command); // OnAffected経由でReloadRails()される

        NewRailName = "";
        NewRailLengthM = 0;
        NewRailSpeedLimitKph = 0;
        NewRailRole = RailRole.Normal;
        NewEndpointAKind = RailEndpointKind.None;
        NewEndpointBKind = RailEndpointKind.None;
    }

    [RelayCommand]
    private void DeleteSelectedRail()
    {
        if (SelectedRail is null) return;

        DeleteRailError = null;
        try
        {
            var command = new DeleteRailCommand(
                _session.Current.Rails, SelectedRail, _session,
                _session.Current.Platforms, _session.Current.TemporaryRestrictions, _session.Current.Trains);
            _invoker.Execute(command); // OnAffected経由でReloadRails()される
            SelectedRail = null;
        }
        catch (InvalidOperationException ex)
        {
            DeleteRailError = ex.Message;
        }
    }

    [RelayCommand]
    private void GoBack() => _goBack();

    public void Dispose() => _bridge.Unsubscribe(this);
}

/// <summary>
/// Rail新規作成UI向けの端点種別選択肢。Switcherは§7.1確定仕様により別導線のためここに含めない。
/// </summary>
public enum RailEndpointKind { None, BoundaryPoint, EntryPoint, BufferStop }