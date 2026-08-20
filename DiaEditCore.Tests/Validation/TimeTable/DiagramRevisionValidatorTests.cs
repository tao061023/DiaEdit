using DiaEditCore.Model;
using DiaEditCore.Model.TimeTable;

using DiaEditCore.Serialization.Validation;
using DiaEditCore.Serialization.Validation.TimeTable;

using Xunit;

namespace DiaEditCore.Tests.Validation.TimeTable;

public class DiagramRevisionValidatorTests
{
    private static TimeTableSet MakeTimeTableSet(int id, string name = "平日") =>
        new() { Id = new TimeTableSetId(id), Name = name, TrainIds = [] };

    [Fact]
    public void baseRevisionIdなし_全TimeTableSetId実在_baseTimeTableSetIdが自身に含まれる場合は合格()
    {
        var set1 = MakeTimeTableSet(1);
        var revision = new DiagramRevision
        {
            Id = new DiagramRevisionId(1),
            Name = "2026年ダイヤ改正",
            TimeTableSetIds = [set1.Id],
            BaseTimeTableSetId = set1.Id,
        };
        var context = new ValidationContext { TimeTableSets = [set1] };

        var issues = new DiagramRevisionValidator().Validate(revision, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void TimeTableSetIdsが空でbaseTimeTableSetIdがnullなら合格()
    {
        // DiagramRevision作成直後、TimeTableSetを1つも持たない編集フロー（設計書1024行目）。
        var revision = new DiagramRevision
        {
            Id = new DiagramRevisionId(1),
            Name = "2026年ダイヤ改正",
            TimeTableSetIds = [],
            BaseTimeTableSetId = null,
        };
        var context = new ValidationContext();

        var issues = new DiagramRevisionValidator().Validate(revision, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 存在しないBaseRevisionIdを参照すると不合格()
    {
        var revision = new DiagramRevision
        {
            Id = new DiagramRevisionId(1),
            Name = "2027年ダイヤ改正",
            BaseRevisionId = new DiagramRevisionId(999),
            TimeTableSetIds = [],
        };
        var context = new ValidationContext { DiagramRevisions = [] };

        var issues = new DiagramRevisionValidator().Validate(revision, context);

        Assert.Contains(issues, i => i.Message.Contains("BaseRevisionId"));
    }

    [Fact]
    public void 存在しないTimeTableSetIdを含むと不合格()
    {
        var revision = new DiagramRevision
        {
            Id = new DiagramRevisionId(1),
            Name = "2026年ダイヤ改正",
            TimeTableSetIds = [new TimeTableSetId(999)],
        };
        var context = new ValidationContext { TimeTableSets = [] };

        var issues = new DiagramRevisionValidator().Validate(revision, context);

        Assert.Contains(issues, i => i.Message.Contains("TimeTableSetIds"));
    }

    [Fact]
    public void BaseTimeTableSetIdがTimeTableSetIdsに含まれないと不合格()
    {
        var set1 = MakeTimeTableSet(1);
        var set2 = MakeTimeTableSet(2, "休日");
        var revision = new DiagramRevision
        {
            Id = new DiagramRevisionId(1),
            Name = "2026年ダイヤ改正",
            TimeTableSetIds = [set1.Id], // set2は含まれない
            BaseTimeTableSetId = set2.Id,
        };
        var context = new ValidationContext { TimeTableSets = [set1, set2] };

        var issues = new DiagramRevisionValidator().Validate(revision, context);

        Assert.Contains(issues, i => i.Message.Contains("BaseTimeTableSetId"));
    }
}