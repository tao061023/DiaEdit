using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.Timetable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class TimeTableSetValidatorTests
{
    private static Train MakeValidTrain(int id, string trainNumber = "1234M") => new()
    {
        Id = new TrainId(id),
        TrainNumber = trainNumber,
        ServiceRouteId = new ServiceRouteId(1),
        TrainTypeId = new TrainTypeId(1),
        TrainTypeName = new DisplayName { Name = "普通" },
        Nickname = new DisplayName { Name = "" },
        DefaultVehicleTypeId = new VehicleTypeId(1),
    };

    [Fact]
    public void TrainIdsが全て実在すれば合格()
    {
        var train = MakeValidTrain(1);
        var set = new TimeTableSet { Id = new TimeTableSetId(1), Name = "平日", TrainIds = [train.Id] };
        var context = new ValidationContext { Trains = [train] };

        var issues = new TimeTableSetValidator().Validate(set, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void TrainIdsが空なら合格()
    {
        var set = new TimeTableSet { Id = new TimeTableSetId(1), Name = "平日", TrainIds = [] };
        var context = new ValidationContext();

        var issues = new TimeTableSetValidator().Validate(set, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 存在しないTrainIdを含むと不合格()
    {
        var train = MakeValidTrain(1);
        var set = new TimeTableSet
        {
            Id = new TimeTableSetId(1),
            Name = "平日",
            TrainIds = [train.Id, new TrainId(999)],
        };
        var context = new ValidationContext { Trains = [train] };

        var issues = new TimeTableSetValidator().Validate(set, context);

        Assert.Contains(issues, i => i.Message.Contains("999"));
    }
}