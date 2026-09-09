namespace DiaEditApp.ViewModels.Stations;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DiaEditApp.ViewModels; // IAffectedByObjectId, ChangeNotificationBridge

using DiaEditCore.Algorithm.Stations;
using DiaEditCore.Algorithm.Stations.FloorUnitObjects;
using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Commands.Stations.FloorUnitObjects;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using DiaEditCore.Model.Stations.FloorUnitObjects;
using DiaEditCore.Session;

/// <summary>
/// UI設計書§4.2.3のキャンバスモード（v13.13確定の3モード制）。
/// StationPathEditは今回スコープ外のためボタン配置のみ（§4.4.2-23）。
/// </summary>
public enum CanvasMode { View, TrackEndpointPlatformEdit, StationPathEdit }

/// <summary>
/// キャンバス表示専用の座標型（double、View非依存）。
/// モデルのPoint（int、モデル空間の生座標）とは区別する。ReloadCanvasShapesで
/// バウンディングボックスに基づきスケール変換された後の「画面表示用座標」を保持する。
/// DiaEditApp.ViewModelsプロジェクトはAvalonia非依存方針のため、Avalonia.Pointを
/// 直接使わずこの型を経由する（FloorUnitCanvasConverters誤配置の教訓、v13.13セッション）。
/// </summary>
public readonly record struct CanvasPoint(double X, double Y);

/// <summary>
/// キャンバス上に描画するRail 1本分の座標情報（表示専用DTO）。
/// </summary>
/// <remarks>
/// Model層のRailを直接バインドせず、解決済み座標を都度計算して持たせることで
/// XAML側からPoint解決ロジック（ResolvePosition呼び出し）を隠蔽する。
/// </remarks>
public sealed record RailCanvasShape(Rail Rail, CanvasPoint A, CanvasPoint B, bool IsSelected);

/// <summary>
/// キャンバス上に描画する端点1件分の座標・種別情報（表示専用DTO）。
/// </summary>
public sealed record EndpointCanvasShape(ObjectId ObjectId, CanvasPoint Position, EndpointVisualKind Kind, bool IsSelected);

/// <summary>端点の視覚表現種別（NoneEndpoint/BoundaryPoint/EntryPoint/BufferStop/Switcherの5種、形状で区別）。</summary>
public enum EndpointVisualKind { None, BoundaryPoint, EntryPoint, BufferStop, Switcher }

/// <summary>
/// UI設計書§4.2.3「構内配線図ポップアップ」のキャンバス実装（ステージ1：描画・モード切替・選択のみ）。
/// </summary>
/// <remarks>
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
///
/// キャンバス（ステージ1）：ドラッグ操作（端点移動・新規Rail作成・範囲選択）はステージ2で対応する。
/// 現段階ではCanvasRails/CanvasEndpointsは表示専用（読み取り）であり、実際の新規作成・属性変更・
/// 削除は既存のフォーム入力系コマンド（AddRail／SaveSelectedRail／DeleteSelectedRail）を通じて行う。
/// </summary>
public sealed partial class FloorUnitDetailViewModel : ViewModelBase, IAffectedByObjectId, IDisposable
{
    public IReadOnlyList<RailRole> RailRoles { get; } = Enum.GetValues<RailRole>();
    public IReadOnlyList<RailEndpointKind> EndpointKinds { get; } = Enum.GetValues<RailEndpointKind>();
    public IReadOnlyList<EntryPointType> EntryPointTypes { get; } = Enum.GetValues<EntryPointType>();
    public IReadOnlyList<CanvasMode> CanvasModes { get; } = Enum.GetValues<CanvasMode>();

    private readonly FloorUnit _floorUnit;
    private readonly ProjectSession _session;
    private readonly CommandInvoker _invoker;
    private readonly ChangeNotificationBridge _bridge;
    private readonly Action _goBack;

    public string FloorUnitName => _floorUnit.Name;

    public ObservableCollection<Rail> Rails { get; } = new();

    // ---- キャンバス描画（ステージ1：閲覧・選択のみ） ----

    public ObservableCollection<RailCanvasShape> CanvasRails { get; } = new();
    public ObservableCollection<EndpointCanvasShape> CanvasEndpoints { get; } = new();

    /// <summary>
    /// キャンバス描画領域の外周余白（画面ピクセル単位）。
    /// </summary>
    private const int CanvasMargin = 20;

    /// <summary>
    /// バウンディングボックスをスケール変換後、収める目標一辺の長さ（画面ピクセル単位、
    /// Marginを含まない描画可能領域）。この値と実際のBorder表示枠（XAML側、現在320px）は
    /// 別々に管理しているため、Border側のサイズを変える場合はこちらも合わせて調整すること。
    /// </summary>
    private const double CanvasDisplayExtent = 280;

    /// <summary>
    /// 内側Canvas（XAML）のWidth/Heightに束縛する、スケール変換後の実サイズ（画面ピクセル単位）。
    /// </summary>
    [ObservableProperty]
    public partial double CanvasContentWidth { get; set; } = CanvasMargin * 2;

    [ObservableProperty]
    public partial double CanvasContentHeight { get; set; } = CanvasMargin * 2;

    [ObservableProperty]
    public partial CanvasMode Mode { get; set; } = CanvasMode.View;

    /// <summary>
    /// 閲覧モードではRail・端点とも選択のみ可（属性パネルは参照専用にする想定、
    /// パネル自体のReadOnly化はステージ2でドラッグ編集導入時にあわせて対応）。
    /// 線路・端点・ホーム編集モードでのみ新規作成・削除・属性変更を許可する。
    /// </summary>
    public bool IsEditableMode => Mode == CanvasMode.TrackEndpointPlatformEdit;

    partial void OnModeChanged(CanvasMode value) => OnPropertyChanged(nameof(IsEditableMode));

    // ---- FloorUnit属性編集（Name、§9.2項目29テンプレート） ----

    [ObservableProperty]
    public partial string EditFloorUnitName { get; set; } = "";

    partial void OnEditFloorUnitNameChanged(string value) => OnPropertyChanged(nameof(IsFloorUnitNameDirty));

    public bool IsFloorUnitNameDirty => EditFloorUnitName != _floorUnit.Name;

    [ObservableProperty]
    public partial FloorUnitSummary Summary { get; set; } = null!;

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

    // ResolveFloorUnitId／RailsBelongingToThisFloorUnitは、FloorUnitSummaryResolver新設に伴い
    // DiaEditCore.Algorithm.Resolvers.RailFloorUnitLookupへ切り出した（単一の情報源原則、
    // v13.11セッションで解消）。呼び出し元はRailsBelongingToThisFloorUnit()を参照。
    private IEnumerable<Rail> RailsBelongingToThisFloorUnit() =>
        RailFloorUnitLookup.RailsBelongingTo(
            _floorUnit.Id, _session.Current.Rails,
            _session.Current.NoneEndpoints, _session.Current.BoundaryPoints,
            _session.Current.EntryPoints, _session.Current.BufferStops, _session.Current.Switchers);

    private void ReloadRails()
    {
        Rails.Clear();
        foreach (var rail in RailsBelongingToThisFloorUnit())
            Rails.Add(rail);
        ReloadSummary();
        ReloadCanvasShapes();
    }

    /// <summary>
    /// キャンバス描画用DTOを再構築する（discard-and-regenerate方針、§9.2項目29と同じ発想：
    /// 差分更新は行わず、変更通知のたびに丸ごと作り直す）。
    /// </summary>
    private void ReloadCanvasShapes()
    {
        CanvasRails.Clear();
        CanvasEndpoints.Clear();

        var none = _session.Current.NoneEndpoints;
        var boundary = _session.Current.BoundaryPoints;
        var entry = _session.Current.EntryPoints;
        var buffer = _session.Current.BufferStops;
        var switchers = _session.Current.Switchers;

        // 1パス目：全Railの生の解決座標（モデル座標系、int）を集める。
        var rawPositions = new List<(Rail Rail, Point A, Point B)>();
        foreach (var rail in Rails)
        {
            var posA = RailEndpointConvergenceResolver.ResolvePosition(rail.EndpointA, none, boundary, entry, buffer, switchers);
            var posB = RailEndpointConvergenceResolver.ResolvePosition(rail.EndpointB, none, boundary, entry, buffer, switchers);
            rawPositions.Add((rail, posA, posB));
        }

        if (rawPositions.Count == 0)
        {
            CanvasContentWidth = CanvasMargin * 2;
            CanvasContentHeight = CanvasMargin * 2;
            return;
        }

        // バウンディングボックスを算出する（モデル座標系、int）。
        var minX = rawPositions.SelectMany(r => new[] { r.A.X, r.B.X }).Min();
        var minY = rawPositions.SelectMany(r => new[] { r.A.Y, r.B.Y }).Min();
        var maxX = rawPositions.SelectMany(r => new[] { r.A.X, r.B.X }).Max();
        var maxY = rawPositions.SelectMany(r => new[] { r.A.Y, r.B.Y }).Max();

        double rangeX = maxX - minX;
        double rangeY = maxY - minY;

        // スケール変換をViewModel側で1回だけ行い、CanvasDisplayExtent四方に収める（アスペクト比維持）。
        // Viewbox等View側の拡大縮小に頼らないことで、StrokeThickness等の画面ピクセル指定値が
        // モデル座標の値域に応じて意図せず縮小・消失する問題（本セッションで発覚）を根本的に回避する。
        double scale = (rangeX, rangeY) switch
        {
            ( <= 0, <= 0) => 1.0, // 全Railが1点に収束（構内配線図として通常あり得ないが、念のため）
            ( <= 0, _) => CanvasDisplayExtent / rangeY,
            (_, <= 0) => CanvasDisplayExtent / rangeX,
            _ => Math.Min(CanvasDisplayExtent / rangeX, CanvasDisplayExtent / rangeY),
        };

        CanvasContentWidth = rangeX * scale + CanvasMargin * 2;
        CanvasContentHeight = rangeY * scale + CanvasMargin * 2;

        CanvasPoint Normalize(Point p) =>
            new((p.X - minX) * scale + CanvasMargin, (p.Y - minY) * scale + CanvasMargin);

        var seenEndpoints = new HashSet<ObjectId>();

        foreach (var (rail, rawA, rawB) in rawPositions)
        {
            var posA = Normalize(rawA);
            var posB = Normalize(rawB);

            CanvasRails.Add(new RailCanvasShape(rail, posA, posB, IsSelected: ReferenceEquals(rail, SelectedRail)));

            AddEndpointShapeIfNew(rail.EndpointA, posA, seenEndpoints);
            AddEndpointShapeIfNew(rail.EndpointB, posB, seenEndpoints);
        }
    }

    /// <summary>
    /// 同一座標に複数Railが収束している場合（BoundaryPoint/Switcher）、端点オブジェクトとしては
    /// 1つしか存在しないため、ObjectId単位で重複描画を防ぐ。
    /// </summary>
    private void AddEndpointShapeIfNew(RailEndpointRef endpointRef, CanvasPoint position, HashSet<ObjectId> seen)
    {
        var (objectId, kind) = endpointRef switch
        {
            NoneEndpointRef n => ((ObjectId)new NoneEndpointObjectId(n.Id), EndpointVisualKind.None),
            BoundaryPointEndpointRef b => (new BoundaryPointObjectId(b.Id), EndpointVisualKind.BoundaryPoint),
            EntryPointEndpointRef e => (new EntryPointObjectId(e.Id), EndpointVisualKind.EntryPoint),
            BufferStopEndpointRef bs => (new BufferStopObjectId(bs.Id), EndpointVisualKind.BufferStop),
            SwitcherEndpointRef sw => (new SwitcherObjectId(sw.Id), EndpointVisualKind.Switcher),
            _ => throw new NotSupportedException($"未知のRailEndpointRef型: {endpointRef.GetType().Name}"),
        };

        if (!seen.Add(objectId)) return;
        CanvasEndpoints.Add(new EndpointCanvasShape(objectId, position, kind, IsSelected: false));
    }

    private void ReloadSummary()
    {
        Summary = FloorUnitSummaryResolver.Build(
            _floorUnit.Id, _session.Current.Rails, _session.Current.StationPaths,
            _session.Current.NoneEndpoints, _session.Current.BoundaryPoints,
            _session.Current.EntryPoints, _session.Current.BufferStops, _session.Current.Switchers);
    }

    void IAffectedByObjectId.OnAffected()
    {
        if (!_session.Current.FloorUnits.Contains(_floorUnit))
        {
            _goBack();
            return;
        }

        EditFloorUnitName = _floorUnit.Name;

        OnPropertyChanged(nameof(FloorUnitName));

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
        ReloadCanvasShapes(); // 選択ハイライト（IsSelected）を再計算するため作り直す
    }

    /// <summary>キャンバス上でRailの図形がクリックされた際、Viewのコードビハインドから呼ばれる。</summary>
    public void SelectRailFromCanvas(Rail rail) => SelectedRail = rail;

    [RelayCommand]
    private void SetViewMode() => Mode = CanvasMode.View;

    [RelayCommand]
    private void SetTrackEditMode() => Mode = CanvasMode.TrackEndpointPlatformEdit;

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
    /// UI設計書§7.1「駅構内オブジェクト新規配置」：Rail作成＝両端点オブジェクトの作成と等価。
    /// RailCreationWorkflowで1つのTransActionCommandとして実行する。
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

    /// <summary>選択中Railの決定と同型の差分判定Save。ChangeFloorUnitAttributesCommandを実行する。</summary>
    [RelayCommand]
    private void SaveFloorUnitName()
    {
        if (!IsFloorUnitNameDirty) return;

        var command = new ChangeFloorUnitAttributesCommand(
            _floorUnit, new FloorUnitSnapshot(EditFloorUnitName), _session);
        _invoker.Execute(command); // OnAffected経由でFloorUnitName等が反映
    }

    [RelayCommand]
    private void GoBack() => _goBack();

    public void Dispose() => _bridge.Unsubscribe(this);
}

/// <summary>
/// Rail新規作成UI向けの端点種別選択肢。Switcherは§7.1確定仕様により別導線のためここに含めない。
/// </summary>
public enum RailEndpointKind { None, BoundaryPoint, EntryPoint, BufferStop }