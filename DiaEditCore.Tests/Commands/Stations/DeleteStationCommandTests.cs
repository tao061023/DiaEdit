namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands;
using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Routes;
using DiaEditCore.Model.Stations;
using DiaEditCore.Session;
using Xunit;

public sealed class DeleteStationCommandTests
{
    private static readonly ValidationRules DefaultValidationRules = new(
        MinDwellTimeSec: null,
        MinHeadwaySec: null,
        MinTurnaroundSec: null,
        TrackEntryMarginSec: null,
        TrackPassMarginSec: null,
        EnableConflictDetection: true,
        EnableCarLengthCheck: true);

    private static ProjectFile MakeEmptyProject() => new()
    {
        SchemaVersion = 1,
        ProjectSettings = new ProjectSettings(DefaultValidationRules),
    };

    private static Station MakeStation(int id) => new()
    {
        Id = new StationId(id),
        DisplayName = new DisplayName { Name = $"駅{id}" },
        Type = StationType.Standard
    };

    private static StationConnectionSegment MakeSeg(int id, int fromStation, int toStation, int fromEp, int toEp) => new()
    {
        Id = new StationConnectionSegmentId(id),
        StationIdA = new StationId(fromStation),
        StationIdB = new StationId(toStation),
        EntryPointIdA = new EntryPointId(fromEp),
        EntryPointIdB = new EntryPointId(toEp),
        MainRouteId = new MainRouteId(1),
    };

    private static StationConnection MakeSc(int id, int mainRouteId, params int[] segIds) => new()
    {
        Id = new StationConnectionId(id),
        Name = $"SC{id}",
        MainRouteId = new MainRouteId(mainRouteId),
        Direction = StationConnectionDirection.Down,
        Segments = segIds.Select(s => new StationConnectionSegmentId(s)).ToList(),
    };

    // v12.21：TimeTableSetCacheを直接newする方式からProjectSession経由へ移行（§9.1項目5）。
    // 旧版は cache.StationConnectionIndex[...] = ... のようにキャッシュへ直接値を注入できたが、
    // ProjectSession経由では_cacheが非公開のためこの手法は使えない。代わりに実際の
    // StationConnection／StationConnectionSegmentをProjectFileへ追加してLoad()し、
    // StationAndEntryPointConnectionIndexBuilderに実際にインデックスを構築させる形に統一する。
    private static ProjectSession MakeSession(ProjectFile project)
    {
        var session = new ProjectSession(new CommandInvoker());
        session.Load(project);
        return session;
    }

    private static ProjectSession EmptySession() => MakeSession(MakeEmptyProject());

    [Fact]
    public void Execute_RemovesStationFromList_WhenNoDirectReferences()
    {
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        var command = new DeleteStationCommand(stations, station, EmptySession());
        command.Execute();

        Assert.Empty(stations);
    }

    [Fact]
    public void Undo_RestoresDeletedStation()
    {
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        var command = new DeleteStationCommand(stations, station, EmptySession());
        command.Execute();
        command.Undo();

        Assert.Single(stations);
        Assert.Same(station, stations[0]);
    }

    [Fact]
    public void Constructor_Throws_WhenDirectReferenceExists()
    {
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        // Station(1)を経由するStationConnectionSegment＋StationConnectionを実際に追加し、
        // StationAndEntryPointConnectionIndexBuilder経由でStationConnectionIndexに
        // 直接の参照元（1ホップ）が登録される状態を再現する。
        var project = MakeEmptyProject();
        project.StationConnectionSegments.Add(MakeSeg(100, fromStation: 1, toStation: 2, fromEp: 10, toEp: 20));
        project.StationConnections.Add(MakeSc(1, mainRouteId: 1, 100));

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteStationCommand(stations, station, MakeSession(project)));
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenNoReferenceExists()
    {
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        // StationConnectionを一切追加しない = Stationへの直接参照は無い状態
        var command = new DeleteStationCommand(stations, station, EmptySession());
        Assert.NotNull(command);
    }

    [Fact]
    public void Execute_DoesNotAffectOtherStations()
    {
        var station1 = MakeStation(1);
        var station2 = MakeStation(2);
        var stations = new List<Station> { station1, station2 };

        var command = new DeleteStationCommand(stations, station1, EmptySession());
        command.Execute();

        Assert.Single(stations);
        Assert.Same(station2, stations[0]);
    }
}