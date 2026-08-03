using DiaEditCore.Model;
using DiaEditCore.Model.Cars;
using DiaEditCore.Serialization.Json;
using DiaEditCore.Serialization.Validation;

using Xunit;

namespace DiaEditCore.Tests.Serialization.Json;

public class JsonProjectFileSerializerTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), $"diaedit_test_{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
        var tmp = _tempPath + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
    }

    private static ProjectFile MakeEmptyValidProject() => new()
    {
        SchemaVersion = JsonProjectFileSerializer.CurrentSchemaVersion,
        ProjectSettings = new ProjectSettings(
            new ValidationRules(null, null, null, null, null, EnableConflictDetection: true, EnableCarLengthCheck: true)),
    };

    [Fact]
    public void 空のProjectFileは保存でき往復変換で内容が保持される()
    {
        var project = MakeEmptyValidProject();

        JsonProjectFileSerializer.Save(project, _tempPath);
        var restored = JsonProjectFileSerializer.Load(_tempPath);

        Assert.Equal(JsonProjectFileSerializer.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Empty(restored.Stations);
        Assert.Empty(restored.Trains);
    }

    [Fact]
    public void ValidationRulesの負値を含むProjectはSaveでProjectFileValidationExceptionになる()
    {
        var project = MakeEmptyValidProject();
        project.ProjectSettings = project.ProjectSettings with
        {
            ValidationRules = project.ProjectSettings.ValidationRules with { MinDwellTimeSec = -1 },
        };

        var ex = Assert.Throws<ProjectFileValidationException>(() => JsonProjectFileSerializer.Save(project, _tempPath));

        Assert.Contains(ex.Issues, i => i.Message.Contains("MinDwellTimeSec"));
        Assert.False(File.Exists(_tempPath)); // 保存不可なのでファイルは作られない
    }

    [Fact]
    public void 検証失敗時は既存の保存ファイルを破壊しない()
    {
        var valid = MakeEmptyValidProject();
        JsonProjectFileSerializer.Save(valid, _tempPath);
        var originalContent = File.ReadAllText(_tempPath);

        var invalid = MakeEmptyValidProject();
        invalid.ProjectSettings = invalid.ProjectSettings with
        {
            ValidationRules = invalid.ProjectSettings.ValidationRules with { MinHeadwaySec = -5 },
        };

        Assert.Throws<ProjectFileValidationException>(() => JsonProjectFileSerializer.Save(invalid, _tempPath));

        // 一時ファイル経由のアトミック置き換えのため、失敗時は元のファイルがそのまま残っているはず
        Assert.Equal(originalContent, File.ReadAllText(_tempPath));
    }

    [Fact]
    public void 未対応のSchemaVersionはLoadでUnsupportedSchemaVersionExceptionになる()
    {
        var project = MakeEmptyValidProject();
        JsonProjectFileSerializer.Save(project, _tempPath);

        // 保存済みJSONのschemaVersionを直接書き換えて未来バージョンを模擬する
        var json = File.ReadAllText(_tempPath);
        var patched = json.Replace(
            $"\"SchemaVersion\": {JsonProjectFileSerializer.CurrentSchemaVersion}",
            "\"SchemaVersion\": 999");
        Assert.NotEqual(json, patched); // 置換が実際に効いていることの前提確認
        File.WriteAllText(_tempPath, patched);

        var ex = Assert.Throws<UnsupportedSchemaVersionException>(() => JsonProjectFileSerializer.Load(_tempPath));
        Assert.Equal(999, ex.FoundVersion);
    }

    [Fact]
    public void CarCompositionのName重複を含むProjectはSaveで保存不可になる()
    {
        // SaveValidationRunnerが実際にCarCompositionValidator（v11.34）まで正しく実行していることの確認
        var project = MakeEmptyValidProject();
        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            VehicleTypeId = new VehicleTypeId(1),
            Type = CarConsistType.Basic,
            Cars = new List<CarRef>(),
        };
        project.CarConsists.Add(consist);
        project.CarCompositions.Add(new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = consist.Id });
        project.CarCompositions.Add(new CarComposition { Id = new CarCompositionId(2), Name = "トウ01", Identifier = 2, CarConsistId = consist.Id }); // Name重複

        var ex = Assert.Throws<ProjectFileValidationException>(() => JsonProjectFileSerializer.Save(project, _tempPath));

        Assert.Contains(ex.Issues, i => i.Message.Contains("Name") && i.Message.Contains("重複"));
    }

    [Fact]
    public void 有効なCarCompositionを含むProjectは往復変換できる()
    {
        var project = MakeEmptyValidProject();
        var car = new Car { Id = new CarId(1), CarType = "テスト車両", IsPower = true, LengthM = 20.0 };
        project.Cars.Add(car);
        var consist = new CarConsist
        {
            Id = new CarConsistId(1),
            VehicleTypeId = new VehicleTypeId(1),
            Type = CarConsistType.Basic,
            Cars = new List<CarRef> { new() { CarId = car.Id, Position = 0 } },
        };
        project.CarConsists.Add(consist);
        project.CarCompositions.Add(new CarComposition { Id = new CarCompositionId(1), Name = "トウ01", Identifier = 1, CarConsistId = consist.Id });

        JsonProjectFileSerializer.Save(project, _tempPath);
        var restored = JsonProjectFileSerializer.Load(_tempPath);

        Assert.Single(restored.CarCompositions);
        Assert.Equal("トウ01", restored.CarCompositions[0].Name);
        Assert.Equal(consist.Id, restored.CarCompositions[0].CarConsistId);
    }
}
