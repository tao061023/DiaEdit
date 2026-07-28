using DiaEditCore.Model;

using DiaEditCore.Serialization.Validation;

using Xunit;

namespace DiaEditCore.Tests.Validation;

public class DisplayNameValidatorTests
{
    private static ValidationContext EmptyContext() => new();

    [Fact]
    public void Nameのみ設定されていれば合格()
    {
        var target = new DisplayName { Name = "東京" };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void Nameが空文字列だと不合格()
    {
        var target = new DisplayName { Name = "" };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("Name"));
    }

    [Fact]
    public void Abbreviationがnullなら合格()
    {
        var target = new DisplayName { Name = "東京", Abbreviation = null };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void Abbreviationが非空文字列なら合格()
    {
        var target = new DisplayName { Name = "東京", Abbreviation = "東" };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void Abbreviationが空文字列だと不合格()
    {
        var target = new DisplayName { Name = "東京", Abbreviation = "" };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("Abbreviation"));
    }

    [Fact]
    public void Translationsのキーが全て小文字なら合格()
    {
        var target = new DisplayName
        {
            Name = "東京",
            Translations = new() { ["en"] = "Tokyo", ["zh-hans"] = "东京" },
        };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Empty(issues);
    }

    [Fact]
    public void Translationsのキーに大文字が含まれると不合格()
    {
        var target = new DisplayName
        {
            Name = "東京",
            Translations = new() { ["EN"] = "Tokyo" },
        };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Contains(issues, i => i.Message.Contains("EN"));
    }

    [Fact]
    public void Translationsの複数キーのうち一部だけ大文字混在でもそのキーのみ検出される()
    {
        var target = new DisplayName
        {
            Name = "東京",
            Translations = new() { ["en"] = "Tokyo", ["Zh-Hans"] = "东京" },
        };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        var issue = Assert.Single(issues);
        Assert.Contains("Zh-Hans", issue.Message);
    }

    [Fact]
    public void Name空とAbbreviation空が同時に発生すると両方検出される()
    {
        var target = new DisplayName { Name = "", Abbreviation = "" };

        var issues = new DisplayNameValidator().Validate(target, EmptyContext());

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Message.Contains("Name"));
        Assert.Contains(issues, i => i.Message.Contains("Abbreviation"));
    }
}
