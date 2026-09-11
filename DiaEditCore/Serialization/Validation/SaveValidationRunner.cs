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
/// 1件でもissueがあれば保存不可（本プロジェクトの運用ではValidationSeverity.Warning＝保存不可相当。）
/// とみなす判断は呼び出し側（JsonProjectFileSerializer.Save）が行う。
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

        // ── クロスバリデータ ──
        issues.AddRange(TrainOperationCrossValidator.Run(context, project.ProjectSettings));
        issues.AddRange(TrainOperationUniquenessValidator.Run(context, project.ProjectSettings));
        issues.AddRange(StationConnectionSegmentOverlapCrossValidator.Run(context));
        issues.AddRange(BaseTimeTableSetTrainDuplicationCrossValidator.Run(context));
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