using System.CommandLine;
using System.CommandLine.Invocation;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Esp.Services;

namespace SpookysAutomod.Cli.Commands;

public static class Fallout4Commands
{
    public static Command Create(Option<bool> jsonOption)
    {
        var root = new Command("fo4", "Fallout 4 ESP/ESM/ESL reading and override patch writing");
        var service = new Fallout4PluginService();

        var create = new Command("create", "Create a new Fallout 4 plugin");
        var output = new Option<string>("--output", "New output plugin file path") { IsRequired = true };
        var light = new Option<bool>("--light", "ESL flag (Fallout 4 desktop; leave off for Fallout 4 VR)");
        var author = new Option<string?>("--author", "Plugin author");
        create.AddOption(output); create.AddOption(light); create.AddOption(author);
        create.SetHandler((InvocationContext ctx) => Print(ctx, service.Create(
            ctx.ParseResult.GetValueForOption(output)!, ctx.ParseResult.GetValueForOption(light),
            ctx.ParseResult.GetValueForOption(author)), ctx.ParseResult.GetValueForOption(jsonOption)));
        root.AddCommand(create);

        var add = new Command("add-record", "Create a new plugin containing a new top-level record");
        var addOutput = new Option<string>("--output", "New output plugin path") { IsRequired = true };
        var addType = new Option<string>("--type", "Mutagen Fallout 4 record class, e.g. Keyword or Armor") { IsRequired = true };
        var editor = new Option<string>("--editor-id", "New record EditorID") { IsRequired = true };
        var fields = new Option<string?>("--changes", "JSON file containing initial scalar field values");
        var addLight = new Option<bool>("--light", "ESL flag (desktop Fallout 4 only)");
        add.AddOption(addOutput); add.AddOption(addType); add.AddOption(editor); add.AddOption(fields); add.AddOption(addLight);
        add.SetHandler((InvocationContext ctx) =>
        {
            var json = ctx.ParseResult.GetValueForOption(jsonOption);
            try
            {
                var fieldsPath = ctx.ParseResult.GetValueForOption(fields);
                Print(ctx, service.CreateRecord(ctx.ParseResult.GetValueForOption(addOutput)!, ctx.ParseResult.GetValueForOption(addType)!,
                    ctx.ParseResult.GetValueForOption(editor)!, fieldsPath == null ? null : File.ReadAllText(fieldsPath),
                    ctx.ParseResult.GetValueForOption(addLight)), json);
            }
            catch (Exception ex) { Print(ctx, Result<string>.Fail(ex.Message), json); }
        });
        root.AddCommand(add);

        foreach (var verb in new[] { "info", "list-records", "find-record", "view-record", "create-override" })
        {
            var command = new Command(verb, $"Fallout 4 {verb}");
            var plugin = new Argument<string>("plugin", "Source plugin path (read-only)");
            var record = new Option<string>("--record", "EditorID or full FormKey (000800:Source.esp)") { IsRequired = true };
            var search = new Option<string?>("--search", "EditorID or FormKey substring");
            var type = new Option<string?>("--type", "Record class, e.g. Weapon, Armor, Npc");
            var patchOutput = new Option<string>("--output", "New patch plugin path") { IsRequired = true };
            var changes = new Option<string?>("--changes", "Path to JSON object of scalar field edits; omit to copy unchanged");
            var patchLight = new Option<bool>("--light", "ESL flag (desktop Fallout 4 only)");
            command.AddArgument(plugin);
            if (verb is "list-records" or "find-record") { command.AddOption(search); command.AddOption(type); }
            if (verb is "view-record" or "create-override") command.AddOption(record);
            if (verb == "create-override") { command.AddOption(patchOutput); command.AddOption(changes); command.AddOption(patchLight); }
            command.SetHandler((InvocationContext ctx) =>
            {
                var source = ctx.ParseResult.GetValueForArgument(plugin);
                var json = ctx.ParseResult.GetValueForOption(jsonOption);
                try
                {
                    switch (verb)
                    {
                        case "info": Print(ctx, service.Info(source), json); break;
                        case "list-records": case "find-record":
                            Print(ctx, service.ListRecords(source, ctx.ParseResult.GetValueForOption(search), ctx.ParseResult.GetValueForOption(type)), json); break;
                        case "view-record": Print(ctx, service.ViewRecord(source, ctx.ParseResult.GetValueForOption(record)!), json); break;
                        case "create-override":
                            var changesPath = ctx.ParseResult.GetValueForOption(changes);
                            Print(ctx, service.CreateOverride(source, ctx.ParseResult.GetValueForOption(record)!,
                                ctx.ParseResult.GetValueForOption(patchOutput)!, changesPath == null ? null : File.ReadAllText(changesPath),
                                ctx.ParseResult.GetValueForOption(patchLight)), json); break;
                    }
                }
                catch (Exception ex) { Print(ctx, Result<string>.Fail(ex.Message), json); }
            });
            root.AddCommand(command);
        }
        return root;
    }

    private static void Print<T>(InvocationContext context, Result<T> result, bool json)
    {
        Console.WriteLine(json || result.Success ? result.ToJson(indented: true) : result.Error);
        context.ExitCode = result.Success ? 0 : 1;
    }
}
