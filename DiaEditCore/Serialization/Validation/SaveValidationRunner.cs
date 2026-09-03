namespace DiaEditCore.Serialization.Validation;

using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation.Stations;
using DiaEditCore.Serialization.Validation.Stations.FloorUnitObjects;
using DiaEditCore.Serialization.Validation.Routes;
using DiaEditCore.Serialization.Validation.Cars;
using DiaEditCore.Serialization.Validation.TimeTable;
using DiaEditCore.Serialization.Validation.TimeTable.Trains;

/// <summary>
/// ProjectFile全体に対して全Validatorを実行し、issueの一覧を返す。
/// 1件でもissueがあれば保存不可（本プロジェクトの運用ではValidationSeverity.Warning＝保存不可相当。
/// 5.13節等参照）とみなす判断は呼び出し側（JsonProjectFileSerializer.Save）が行う。
///
/// 実装状況・既知のギャップ（v11.38、①ProjectFile設計セッションで判明）：
///   - DisplayNameValidatorは特定の集約トップレベルコレクションを持たない（DisplayName型を
///     フィールドとして持つ他オブジェクトの内部で個別に呼ばれる想定）ため、本Runnerでは呼び出さない。
///     各Validator（StationValidator等）が自身のDisplayNameフィールドについて委譲済みかどうかは
///     未確認。呼び出し漏れがないか次回確認が必要（§8.2への追加候補）。
///   - InsertionConfigValidatorはInsertionConfig自体がv11.32でスコープ外・凍結されているため、
///     対応するコレクションがProjectFileに存在せず、意図的に呼び出さない。
///   - Train横断検証（Rule 2）はTrainOperationCrossValidator.Runへ委譲する（v11.38で確認済み。
///     TrainOperationCrossValidator自体が「単一オブジェクトValidatorの契約に収まらない検証」専用の
///     個別呼び出しランナーとして既に実装されていたため、本Runner側で重複実装しない）。
///   - TrainOperationsコレクション自体（TrainOperation.OperationNumberの非空チェック等）を
///     直接検証するValidatorが現時点で存在しない。必要になった時点で追加する。
/// </summary>
public static class SaveValidationRunner
{
    public static IReadOnlyList<IValidationIssue> ValidateAll(ProjectFile project)
    {
        var issues = new List<IValidationIssue>();
        var context = project.ToValidationContext();

        void Run<T>(IValidator<T> validator, IEnumerable<T> targets)
        {
            foreach (var target in targets)
                issues.AddRange(validator.Validate(target, context));
        }

        // ── 駅構内オブジェクト ──
        Run(new StationValidator(), project.Stations);
        Run(new FloorUnitValidator(), project.FloorUnits);
        Run(new RailValidator(), project.Rails);
        Run(new EntryPointValidator(), project.EntryPoints);
        Run(new BoundaryPointValidator(), project.BoundaryPoints);
        Run(new SwitcherValidator(), project.Switchers);
        Run(new BufferStopValidator(), project.BufferStops);
        Run(new PlatformValidator(), project.Platforms);
        Run(new StationPathValidator(), project.StationPaths);

        // ── 路線網 ──
        Run(new StationConnectionSegmentValidator(), project.StationConnectionSegments);
        Run(new MainRouteValidator(), project.MainRoutes);
        Run(new StationConnectionValidator(), project.StationConnections);
        Run(new ServiceRouteValidator(), project.ServiceRoutes);

        // ── 車両 ──
        Run(new CarValidator(), project.Cars);
        Run(new CarConsistValidator(), project.CarConsists);
        Run(new CarCompositionValidator(), project.CarCompositions);
        Run(new VehicleTypeValidator(), project.VehicleTypes);

        // ── 時刻表 ──
        Run(new TrainTypeValidator(), project.TrainTypes);
        Run(new TrainValidator(), project.Trains);
        Run(new TimeTableSetValidator(), project.TimeTableSets);
        Run(new DiagramRevisionValidator(), project.DiagramRevisions);
        Run(new TemporaryRestrictionValidator(), project.TemporaryRestrictions);
        Run(new DisplayContextValidator(), project.DisplayContexts);
        Run(new TrainOperationNonEmptyValidator(), project.TrainOperations);

        // ── プロジェクト設定（単一オブジェクト） ──
        issues.AddRange(new ProjectSettingsValidator().Validate(project.ProjectSettings, context));

        // ── Train横断検証（Rule 2：PrevTrainの運用番号継続性） ──
        // TrainOperationCrossValidator.Runが「単一オブジェクトValidatorの契約に収まらない検証」
        // 専用の個別呼び出しランナーとして既に実装済み（TrainOperationChainResolver・
        // TrainConnectionResolverによるTrainCrossValidationData構築込み）のため、それをそのまま使う。
        issues.AddRange(TrainOperationCrossValidator.Run(context, project.ProjectSettings));

        // ── TrainOperation横断検証（OperationNumberのTimeTableSet単位一意性、§8.2項目15） ──
        issues.AddRange(TrainOperationUniquenessValidator.Run(context, project.ProjectSettings));

        // ── StationConnectionSegment非共有制約の横断検証（4.6.1節、5.13.5節） ──
        // 設計書v12.20の変更履歴では「SaveValidationRunner.ValidateAllへ配線済み」と記載されて
        // いたが、v12.27セッションでの実装作業中に本Runnerへの実際の呼び出しが存在しないことが
        // 判明した（配線漏れ、原因未特定）。複線区間で同一StationConnectionSegmentが2件以上の
        // StationConnectionから参照される不整合を検出できていなかった状態のため、本セッションで復旧する。
        issues.AddRange(StationConnectionSegmentOverlapCrossValidator.Run(context));

        // ── RunTimeCalculator基準実績の一意性検証（5.6.1節、§9.1項目21、v12.27新設） ──
        // DiagramRevision.BaseTimeTableSetId内で、同一選定キー
        // (StationConnectionSegmentId, FromIsStop, ToIsStop, DefaultVehicleTypeId)に
        // 該当するTrainが2件以上存在しないことを検証する。
        // StationConnectionSegmentOverlapCrossValidator等と同じ「単一オブジェクトValidatorの
        // 契約に収まらない検証」向けの静的Runパターンを踏襲し、ProjectSettings非依存のため
        // contextのみを渡す。
        issues.AddRange(BaseTimeTableSetTrainDuplicationCrossValidator.Run(context));

        // ── Rail端点オブジェクトの被参照数上限検証（§9.2項目17派生、v13.7新設） ──
        // 同一のBufferStop/EntryPoint/BoundaryPoint/Switcherポートに、物理的に不可能な本数の
        // Railが接続されていないかを検証する。RailValidator（単一Rail向け）では他Railとの
        // 重複を検知できないため、Cross Validatorとして独立させている。
        issues.AddRange(RailEndpointCardinalityCrossValidator.Run(context));

        return issues;
    }

    /// <summary>ProjectFile（保存の実体、List＋setter）からValidationContext（検証専用の参照束、IReadOnlyList＋init）への変換。
    /// フィールド追加時の対応漏れを避けるため、変換ロジックをこの1箇所に集約する。</summary>
    private static ValidationContext ToValidationContext(this ProjectFile project) => new()
    {
        Stations = project.Stations,
        FloorUnits = project.FloorUnits,
        Rails = project.Rails,
        EntryPoints = project.EntryPoints,
        BoundaryPoints = project.BoundaryPoints,
        Switchers = project.Switchers,
        BufferStops = project.BufferStops,
        StationPaths = project.StationPaths,
        StationConnectionSegments = project.StationConnectionSegments,
        MainRoutes = project.MainRoutes,
        StationConnections = project.StationConnections,
        ServiceRoutes = project.ServiceRoutes,
        Cars = project.Cars,
        CarConsists = project.CarConsists,
        CarCompositions = project.CarCompositions,
        VehicleTypes = project.VehicleTypes,
        TrainTypes = project.TrainTypes,
        Trains = project.Trains,
        TimeTableSets = project.TimeTableSets,
        DiagramRevisions = project.DiagramRevisions,
        TemporaryRestrictions = project.TemporaryRestrictions,
        DisplayContexts = project.DisplayContexts,
        TrainOperations = project.TrainOperations,
        Platforms = project.Platforms,
    };
}