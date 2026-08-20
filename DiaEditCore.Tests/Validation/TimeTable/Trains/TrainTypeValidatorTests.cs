using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable.Trains;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.TimeTable.Trains;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable.Trains;

public class TrainTypeValidatorTests
{
    private static readonly ValidationContext EmptyContext = new();

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#00ff00")]
    [InlineData("#123ABC")]
    public void DiagramColorが正しい形式なら合格(string color)
    {
        var tt = new TrainType
        {
            Id = new TrainTypeId(1),
            Name = new DisplayName { Name = "快速" },
            DiagramColor = color,
            DiagramLineStyle = LineStyle.Solid,
            SortOrder = 0,
        };

        var issues = new TrainTypeValidator().Validate(tt, EmptyContext);

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("FF0000")]
    [InlineData("#FF00")]
    [InlineData("red")]
    [InlineData("")]
    public void DiagramColorが不正な形式なら不合格(string color)
    {
        var tt = new TrainType
        {
            Id = new TrainTypeId(1),
            Name = new DisplayName { Name = "快速" },
            DiagramColor = color,
            DiagramLineStyle = LineStyle.Solid,
            SortOrder = 0,
        };

        var issues = new TrainTypeValidator().Validate(tt, EmptyContext);

        Assert.Single(issues);
    }
}
