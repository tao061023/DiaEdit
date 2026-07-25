using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Validation;

public class DisplayContextValidatorTests
{
    private static MainRoute MakeMainRoute(int id, int stationCount)
    {
        var stationOrder = Enumerable.Range(1, stationCount)
            .Select(i => new StationId(i))
            .ToList();

        return new MainRoute
        {
            Id = new MainRouteId(id),
            Name = new DisplayName { Name = "テスト線" },
            StationOrder = stationOrder,
        };
    }

    [Fact]
    public void MainRouteが実在しFromIndex_ToIndexが範囲内なら合格()
    {
        var route = MakeMainRoute(1, stationCount: 5);
        var dc = new DisplayContext(
            new DisplayContextId(1),
            new DisplayName { Name = "本線ダイヤ" },
            [new MainRouteRange(route.Id, 0, 4)]);
        var context = new ValidationContext { MainRoutes = [route] };

        var issues = new DisplayContextValidator().Validate(dc, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void MainRouteRangesが空だと不合格()
    {
        var dc = new DisplayContext(
            new DisplayContextId(1),
            new DisplayName { Name = "空のDisplayContext" },
            []);
        var context = new ValidationContext();

        var issues = new DisplayContextValidator().Validate(dc, context);

        Assert.Contains(issues, i => i.Message.Contains("MainRouteRanges"));
    }

    [Fact]
    public void 存在しないMainRouteIdを参照すると不合格()
    {
        var dc = new DisplayContext(
            new DisplayContextId(1),
            new DisplayName { Name = "テスト" },
            [new MainRouteRange(new MainRouteId(999), 0, 1)]);
        var context = new ValidationContext { MainRoutes = [] };

        var issues = new DisplayContextValidator().Validate(dc, context);

        Assert.Contains(issues, i => i.Message.Contains("MainRoute") && i.Message.Contains("999"));
    }

    [Fact]
    public void FromIndexが負だと不合格()
    {
        var route = MakeMainRoute(1, stationCount: 5);
        var dc = new DisplayContext(
            new DisplayContextId(1),
            new DisplayName { Name = "テスト" },
            [new MainRouteRange(route.Id, -1, 3)]);
        var context = new ValidationContext { MainRoutes = [route] };

        var issues = new DisplayContextValidator().Validate(dc, context);

        Assert.Contains(issues, i => i.Message.Contains("FromIndex"));
    }

    [Fact]
    public void ToIndexがStationOrder範囲を超えると不合格()
    {
        var route = MakeMainRoute(1, stationCount: 5);
        var dc = new DisplayContext(
            new DisplayContextId(1),
            new DisplayName { Name = "テスト" },
            [new MainRouteRange(route.Id, 0, 5)]); // 有効範囲は0〜4
        var context = new ValidationContext { MainRoutes = [route] };

        var issues = new DisplayContextValidator().Validate(dc, context);

        Assert.Contains(issues, i => i.Message.Contains("ToIndex"));
    }

    [Fact]
    public void 複数MainRouteRangeのうち一つでも不正なら該当分だけ不合格が積まれる()
    {
        var route1 = MakeMainRoute(1, stationCount: 3);
        var route2 = MakeMainRoute(2, stationCount: 3);
        var dc = new DisplayContext(
            new DisplayContextId(1),
            new DisplayName { Name = "直通系統" },
            [
                new MainRouteRange(route1.Id, 0, 2),   // 正常
                new MainRouteRange(route2.Id, 0, 10),  // ToIndex不正
            ]);
        var context = new ValidationContext { MainRoutes = [route1, route2] };

        var issues = new DisplayContextValidator().Validate(dc, context);

        Assert.Single(issues);
        Assert.Contains(issues, i => i.Message.Contains("ToIndex"));
    }
}