using DiaEditCore.Model;
using DiaEditCore.Serialization.Validation;
using Xunit;

namespace DiaEditCore.Tests.Serialization.Validation;

public class ProjectSettingsValidatorTests
{
    private static ValidationRules MakeValidRules() => new(
        MinDwellTimeSec: 30,
        MinHeadwaySec: 120,
        MinTurnaroundSec: 300,
        TrackEntryMarginSec: 60,
        TrackPassMarginSec: 10,
        EnableConflictDetection: true,
        EnableCarLengthCheck: true);

    [Fact]
    public void 全フィールドが非負なら合格()
    {
        var settings = new ProjectSettings(MakeValidRules(), 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void 全フィールドがnullでも合格()
    {
        var rules = new ValidationRules(
            MinDwellTimeSec: null,
            MinHeadwaySec: null,
            MinTurnaroundSec: null,
            TrackEntryMarginSec: null,
            TrackPassMarginSec: null,
            EnableConflictDetection: false,
            EnableCarLengthCheck: false);
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Empty(issues);
    }

    [Fact]
    public void MinDwellTimeSecが負だと不合格()
    {
        var rules = MakeValidRules() with { MinDwellTimeSec = -1 };
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Contains(issues, i => i.Message.Contains("MinDwellTimeSec"));
    }

    [Fact]
    public void MinHeadwaySecが負だと不合格()
    {
        var rules = MakeValidRules() with { MinHeadwaySec = -1 };
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Contains(issues, i => i.Message.Contains("MinHeadwaySec"));
    }

    [Fact]
    public void MinTurnaroundSecが負だと不合格()
    {
        var rules = MakeValidRules() with { MinTurnaroundSec = -1 };
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Contains(issues, i => i.Message.Contains("MinTurnaroundSec"));
    }

    [Fact]
    public void TrackEntryMarginSecが負だと不合格()
    {
        var rules = MakeValidRules() with { TrackEntryMarginSec = -1 };
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Contains(issues, i => i.Message.Contains("TrackEntryMarginSec"));
    }

    [Fact]
    public void TrackPassMarginSecが負だと不合格()
    {
        var rules = MakeValidRules() with { TrackPassMarginSec = -1 };
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Contains(issues, i => i.Message.Contains("TrackPassMarginSec"));
    }

    [Fact]
    public void 複数フィールドが同時に負だと該当分の不合格が積まれる()
    {
        var rules = MakeValidRules() with { MinDwellTimeSec = -1, MinHeadwaySec = -5 };
        var settings = new ProjectSettings(rules, 14400);
        var context = new ValidationContext();

        var issues = new ProjectSettingsValidator().Validate(settings, context);

        Assert.Equal(2, issues.Count);
    }
}
