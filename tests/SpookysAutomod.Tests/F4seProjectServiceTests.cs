using SpookysAutomod.Skse.Services;

namespace SpookysAutomod.Tests;

public sealed class F4seProjectServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "SpookyF4se_" + Guid.NewGuid().ToString("N"));
    private readonly F4seProjectService service = new();
    public F4seProjectServiceTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private string FakeSdk(bool vr)
    {
        var sdk = Path.Combine(root, "sdk", "extender");
        Directory.CreateDirectory(Path.Combine(sdk, "f4se"));
        Directory.CreateDirectory(Path.Combine(sdk, "f4se_common"));
        Directory.CreateDirectory(Path.Combine(root, "sdk", "common"));
        File.WriteAllText(Path.Combine(sdk, "f4se", "PluginAPI.h"), "// fixture; native integration uses the real SDK");
        File.WriteAllText(Path.Combine(root, "sdk", "common", "ITypes.h"), "");
        File.WriteAllText(Path.Combine(sdk, "f4se_common", "f4se_version.h"), "#define CURRENT_RELEASE_RUNTIME " + (vr ? "RUNTIME_VR_VERSION_1_2_72" : "RUNTIME_VERSION_1_10_163"));
        return sdk;
    }
    [Theory]
    [InlineData("vr", true, "0x01020482u")]
    [InlineData("desktop-legacy", false, "0x010A0A30u")]
    public void CreatesTargetSpecificProjectAndPreservesExistingOutput(string target, bool vr, string runtime)
    {
        var sdk = FakeSdk(vr);
        var created = service.Create("TestPlugin", root, target, sdk);
        Assert.True(created.Success, created.Error);
        var source = File.ReadAllText(Path.Combine(created.Value!, "main.cpp"));
        Assert.Contains(runtime, source);
        Assert.DoesNotContain("@NAME@", source);
        Assert.DoesNotContain("@RUNTIME@", source);
        Assert.True(File.Exists(Path.Combine(created.Value!, "host.cpp")));
        Assert.Equal(target, service.Info(created.Value!).Value!.Target);
        Assert.False(service.Create("TestPlugin", root, target, sdk).Success);
        Assert.Equal(source, File.ReadAllText(Path.Combine(created.Value!, "main.cpp")));
    }
    [Theory]
    [InlineData("../escape")]
    [InlineData("Bad;Name")]
    [InlineData("Bad\"Name")]
    public void RejectsUnsafeNames(string name)
        => Assert.False(service.Create(name, root, "vr", FakeSdk(true)).Success);
    [Fact]
    public void RejectsWrongSdkAndUnknownTargets()
    {
        var sdk = FakeSdk(true);
        Assert.False(service.Create("WrongSdk", root, "desktop-legacy", sdk).Success);
        Assert.False(service.Create("WrongTarget", root, "next-gen", sdk).Success);
        Assert.False(Directory.Exists(Path.Combine(root, "WrongSdk")));
    }
    [Fact]
    public async Task MissingConfigurationFailsBeforeLaunchingBuild()
        => Assert.False((await service.Build(root)).Success);
}
