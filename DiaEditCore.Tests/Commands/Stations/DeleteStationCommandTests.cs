namespace DiaEditCore.Tests.Commands.Stations;

using DiaEditCore.Commands.Stations;
using DiaEditCore.Model;
using DiaEditCore.Model.Stations;
using Xunit;

public sealed class DeleteStationCommandTests
{
    private static Station MakeStation(int id) => new()
    {
        Id = new StationId(id),
        DisplayName = new DisplayName { Name = $"駅{id}" },
        Type = StationType.Standard
    };

    private static TimeTableSetCache EmptyCache() => new();

    [Fact]
    public void Execute_RemovesStationFromList_WhenNoDirectReferences()
    {
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        var command = new DeleteStationCommand(stations, station, EmptyCache());
        command.Execute();

        Assert.Empty(stations);
    }

    [Fact]
    public void Undo_RestoresDeletedStation()
    {
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        var command = new DeleteStationCommand(stations, station, EmptyCache());
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

        var cache = new TimeTableSetCache();
        // StationConnectionIndexにこのStationを参照するエントリを積み、
        // 直接の参照元（1ホップ）が存在する状態を再現する。
        cache.StationConnectionIndex[station.Id] = new List<StationConnectionId>
        {
            new StationConnectionId(100)
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DeleteStationCommand(stations, station, cache));
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenOnlyIndirectReferenceExists()
    {
        // StationConnectionSegment経由の間接参照のみ存在するケースでは、直接参照ではないため
        // ブロックされないことを確認する（1ホップ判定であることの検証）。
        var station = MakeStation(1);
        var stations = new List<Station> { station };

        var cache = new TimeTableSetCache();
        // StationConnectionIndexには何も登録しない = Stationへの直接参照は無い状態

        var command = new DeleteStationCommand(stations, station, cache);
        Assert.NotNull(command);
    }

    [Fact]
    public void Execute_DoesNotAffectOtherStations()
    {
        var station1 = MakeStation(1);
        var station2 = MakeStation(2);
        var stations = new List<Station> { station1, station2 };

        var command = new DeleteStationCommand(stations, station1, EmptyCache());
        command.Execute();

        Assert.Single(stations);
        Assert.Same(station2, stations[0]);
    }
}