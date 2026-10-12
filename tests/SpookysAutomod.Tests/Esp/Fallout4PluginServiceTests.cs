using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using SpookysAutomod.Esp.Services;

namespace SpookysAutomod.Tests.Esp;

public sealed class Fallout4PluginServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SpookyFo4Tests_" + Guid.NewGuid().ToString("N"));
    private readonly Fallout4PluginService service = new();
    public Fallout4PluginServiceTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    private string PathFor(string name) => Path.Combine(directory, name);

    private string Source()
    {
        var mod = new Fallout4Mod(ModKey.FromFileName("Source.esp"), Fallout4Release.Fallout4);
        var keyword = mod.Keywords.AddNew(); keyword.EditorID = "TestKeyword";
        var armor = mod.Armors.AddNew(); armor.EditorID = "TestArmor"; armor.Name = "Original armor";
        armor.Keywords = new() { keyword.ToLink<IKeywordGetter>() };
        mod.WriteToBinary(PathFor("Source.esp"));
        return PathFor("Source.esp");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateRoundTripsHeader(bool light)
    {
        var result = service.Create(PathFor("New.esp"), light, "TestAuthor");
        Assert.True(result.Success, result.Error);
        using var mod = Fallout4Mod.CreateFromBinaryOverlay(result.Value!, Fallout4Release.Fallout4);
        Assert.Equal("TestAuthor", mod.ModHeader.Author);
        Assert.Equal(light, mod.ModHeader.Flags.HasFlag(Fallout4ModHeader.HeaderFlag.Small));
    }

    [Fact]
    public void OverridePreservesSourceFormKeysLinksAndUntouchedFields()
    {
        var source = Source(); var bytes = File.ReadAllBytes(source);
        var result = service.CreateOverride(source, "TestArmor", PathFor("Patch.esp"), "{\"EditorID\":\"PatchedArmor\"}", true);
        Assert.True(result.Success, result.Error);
        using var original = Fallout4Mod.CreateFromBinaryOverlay(source, Fallout4Release.Fallout4);
        using var patch = Fallout4Mod.CreateFromBinaryOverlay(result.Value!, Fallout4Release.Fallout4);
        var armor = Assert.Single(patch.Armors);
        Assert.Equal(original.Armors.First().FormKey, armor.FormKey);
        Assert.Equal("PatchedArmor", armor.EditorID);
        Assert.Equal("Original armor", armor.Name!.String);
        Assert.Equal(original.Armors.First().Keywords![0].FormKey, armor.Keywords![0].FormKey);
        Assert.Contains(patch.ModHeader.MasterReferences, m => m.Master == original.ModKey);
        Assert.Empty(patch.Keywords);
        Assert.Equal(bytes, File.ReadAllBytes(source));
    }

    [Fact]
    public void InspectAndFindRealSerializedRecords()
    {
        var source = Source();
        Assert.True(service.Info(source).Success);
        var list = service.ListRecords(source, "Armor", "Armor");
        Assert.True(list.Success, list.Error);
        Assert.Contains("TestArmor", list.ToJson());
        Assert.DoesNotContain("TestKeyword", list.ToJson());
        var view = service.ViewRecord(source, "TestArmor");
        Assert.True(view.Success, view.Error);
        Assert.Contains("Original armor", view.ToJson());
    }

    [Theory]
    [InlineData("{\"FormKey\":\"000999:Other.esp\"}")]
    [InlineData("{\"Nonexistent\":2}")]
    [InlineData("{\"Keywords\":[]}")]
    [InlineData("[]")]
    public void InvalidEditsDoNotProduceOutput(string edits)
    {
        var result = service.CreateOverride(Source(), "TestArmor", PathFor("Bad.esp"), edits);
        Assert.False(result.Success);
        Assert.False(File.Exists(PathFor("Bad.esp")));
    }

    [Fact]
    public void ExistingOutputAndSameModKeyAreRejected()
    {
        var source = Source(); var bytes = File.ReadAllBytes(source);
        Assert.False(service.Create(source).Success);
        Assert.False(service.CreateOverride(source, "TestArmor", source).Success);
        Assert.False(service.CreateOverride(source, "TestArmor", Path.Combine(directory, "other", "Source.esp")).Success);
        Assert.Equal(bytes, File.ReadAllBytes(source));
    }

    [Fact]
    public void CreatesNewRecordAndEditsNumericAndDisplayNameFields()
    {
        var created = service.CreateRecord(PathFor("Armor.esp"), "Armor", "NewArmor", "{\"Name\":\"Test armor\",\"Weight\":4.5,\"Value\":25}", true);
        Assert.True(created.Success, created.Error);
        using var mod = Fallout4Mod.CreateFromBinaryOverlay(created.Value!, Fallout4Release.Fallout4);
        var armor = Assert.Single(mod.Armors);
        Assert.Equal("Test armor", armor.Name!.String);
        Assert.Equal(4.5f, armor.Weight);
        Assert.Equal(25, armor.Value);
        Assert.Equal(mod.ModKey, armor.FormKey.ModKey);
        Assert.InRange(armor.FormKey.ID, 0x800u, 0xFFFu);
        var edited = service.CreateOverride(created.Value!, "NewArmor", PathFor("Renamed.esp"), "{\"Name\":\"Renamed armor\",\"Weight\":2.0}");
        Assert.True(edited.Success, edited.Error);
        using var patch = Fallout4Mod.CreateFromBinaryOverlay(edited.Value!, Fallout4Release.Fallout4);
        Assert.Equal("Renamed armor", patch.Armors.First().Name!.String);
        Assert.Equal(2f, patch.Armors.First().Weight);
        Assert.Equal(25, patch.Armors.First().Value);
    }

    [Fact]
    public void MissingAndCorruptInputsReturnErrors()
    {
        Assert.False(service.Info(PathFor("Missing.esp")).Success);
        File.WriteAllText(PathFor("Corrupt.esp"), "not a plugin");
        Assert.False(service.Info(PathFor("Corrupt.esp")).Success);
        Assert.False(service.CreateOverride(Source(), "MissingRecord", PathFor("Bad.esp")).Success);
    }
}
