using System.CommandLine;
using System.CommandLine.Invocation;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Skse.Services;

namespace SpookysAutomod.Cli.Commands;

public static class F4seCommands
{
    public static Command Create(Option<bool> json)
    {
        var root = new Command("f4se", "Create and build Fallout 4 VR / legacy desktop C++ DLL plugins");
        var service = new F4seProjectService();
        var create = new Command("create", "Create a DLL project with a standalone mock-host test");
        var name = new Argument<string>("name", "C++ project name: letters, digits, underscores");
        var output = new Option<string>("--output", () => ".", "Parent output directory");
        var target = new Option<string>("--target", "vr (1.2.72) or desktop-legacy (1.10.163)") { IsRequired = true };
        var sdk = new Option<string>("--sdk", "Official SDK source directory containing f4se and f4se_common") { IsRequired = true };
        create.AddArgument(name); create.AddOption(output); create.AddOption(target); create.AddOption(sdk);
        create.SetHandler((InvocationContext ctx) => Print(ctx, service.Create(ctx.ParseResult.GetValueForArgument(name),
            ctx.ParseResult.GetValueForOption(output)!, ctx.ParseResult.GetValueForOption(target)!, ctx.ParseResult.GetValueForOption(sdk)!), ctx.ParseResult.GetValueForOption(json)));
        root.AddCommand(create);
        var info = new Command("info", "Read project target and validate its SDK");
        var infoPath = new Argument<string>("project"); info.AddArgument(infoPath);
        info.SetHandler((InvocationContext ctx) => Print(ctx, service.Info(ctx.ParseResult.GetValueForArgument(infoPath)), ctx.ParseResult.GetValueForOption(json)));
        root.AddCommand(info);
        var build = new Command("build", "Compile x64 Release DLL, run mock-host test, and stage an MO2 package");
        var buildPath = new Argument<string>("project"); build.AddArgument(buildPath);
        build.SetHandler(async (InvocationContext ctx) => Print(ctx, await service.Build(ctx.ParseResult.GetValueForArgument(buildPath)), ctx.ParseResult.GetValueForOption(json)));
        root.AddCommand(build);
        return root;
    }
    private static void Print<T>(InvocationContext ctx, Result<T> result, bool json)
    {
        Console.WriteLine(json || result.Success ? result.ToJson(true) : result.Error);
        ctx.ExitCode = result.Success ? 0 : 1;
    }
}
