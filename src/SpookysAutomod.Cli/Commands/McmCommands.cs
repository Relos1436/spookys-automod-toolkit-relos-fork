using System.CommandLine;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Mcm.Services;

namespace SpookysAutomod.Cli.Commands;

public static class McmCommands
{
    private static Option<bool> _jsonOption = null!;
    private static Option<bool> _verboseOption = null!;

    public static Command Create(Option<bool> jsonOption, Option<bool> verboseOption)
    {
        _jsonOption = jsonOption;
        _verboseOption = verboseOption;

        var mcmCommand = new Command("mcm", "MCM Helper configuration operations");

        mcmCommand.AddCommand(CreateCreateCommand());
        mcmCommand.AddCommand(CreateInfoCommand());
        mcmCommand.AddCommand(CreateValidateCommand());
        mcmCommand.AddCommand(CreateAddToggleCommand());
        mcmCommand.AddCommand(CreateAddSliderCommand());
        mcmCommand.AddCommand(CreateGenerateSettingsCommand());
        mcmCommand.AddCommand(CreateAddQuestCommand());

        return mcmCommand;
    }

    private static IModLogger CreateLogger(bool json, bool verbose) =>
        json ? new SilentLogger() : new ConsoleLogger(verbose);

    private static Command CreateCreateCommand()
    {
        var modNameArg = new Argument<string>("modName", "Internal mod name (no spaces)");
        var displayNameArg = new Argument<string>("displayName", "Display name shown in MCM");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "./config.json",
            description: "Output file path");

        var cmd = new Command("create", "Create a new MCM configuration file")
        {
            modNameArg,
            displayNameArg,
            outputOption
        };

        cmd.SetHandler((modName, displayName, output, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new McmService(logger);

            var result = service.Create(modName, displayName, output);

            if (json)
            {
                if (result.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            outputPath = result.Value
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(Result.Fail(result.Error!).ToJson(true));
                }
            }
            else if (result.Success)
            {
                Console.WriteLine($"Created MCM config: {result.Value}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, modNameArg, displayNameArg, outputOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateInfoCommand()
    {
        var configArg = new Argument<string>("config", "Path to the MCM config file");

        var cmd = new Command("info", "Get information about an MCM config")
        {
            configArg
        };

        cmd.SetHandler((config, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new McmService(logger);

            var result = service.GetInfo(config);

            if (json)
            {
                if (result.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            modName = result.Value!.ModName,
                            displayName = result.Value.DisplayName,
                            minMcmVersion = result.Value.MinMcmVersion,
                            pageCount = result.Value.PageCount,
                            controlCount = result.Value.ControlCount,
                            pages = result.Value.Pages.Select(p => new
                            {
                                name = p.Name,
                                controlCount = p.ControlCount
                            })
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(Result.Fail(result.Error!).ToJson(true));
                }
            }
            else if (result.Success)
            {
                var info = result.Value!;
                Console.WriteLine($"MCM Config: {info.DisplayName}");
                Console.WriteLine($"Internal Name: {info.ModName}");
                Console.WriteLine($"Min MCM Version: {info.MinMcmVersion}");
                Console.WriteLine($"Pages: {info.PageCount}");
                Console.WriteLine($"Controls: {info.ControlCount}");

                if (info.Pages.Count > 0 && verbose)
                {
                    Console.WriteLine("\nPages:");
                    foreach (var page in info.Pages)
                    {
                        Console.WriteLine($"  - {page.Name} ({page.ControlCount} controls)");
                    }
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, configArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateValidateCommand()
    {
        var configArg = new Argument<string>("config", "Path to the MCM config file");

        var cmd = new Command("validate", "Validate an MCM config file")
        {
            configArg
        };

        cmd.SetHandler((config, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new McmService(logger);

            var result = service.Validate(config);

            if (json)
            {
                if (result.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            isValid = result.Value!.IsValid,
                            errors = result.Value.Errors,
                            warnings = result.Value.Warnings
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(Result.Fail(result.Error!).ToJson(true));
                }
            }
            else if (result.Success)
            {
                var validation = result.Value!;
                if (validation.IsValid)
                {
                    Console.WriteLine($"Validation passed: {config}");
                    if (validation.Warnings.Count > 0)
                    {
                        Console.WriteLine("\nWarnings:");
                        foreach (var w in validation.Warnings)
                            Console.WriteLine($"  - {w}");
                    }
                }
                else
                {
                    Console.WriteLine($"Validation failed: {config}");
                    Console.WriteLine("\nErrors:");
                    foreach (var e in validation.Errors)
                        Console.WriteLine($"  - {e}");
                    if (validation.Warnings.Count > 0)
                    {
                        Console.WriteLine("\nWarnings:");
                        foreach (var w in validation.Warnings)
                            Console.WriteLine($"  - {w}");
                    }
                    Environment.ExitCode = 1;
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, configArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddToggleCommand()
    {
        var configArg = new Argument<string>("config", "Path to the MCM config file");
        var idArg = new Argument<string>("id", "Control identifier");
        var textArg = new Argument<string>("text", "Display text");
        var helpOption = new Option<string?>("--help-text", "Help text shown on hover");
        var pageOption = new Option<string?>("--page", "Target page name");

        var cmd = new Command("add-toggle", "Add a toggle control to MCM config")
        {
            configArg,
            idArg,
            textArg,
            helpOption,
            pageOption
        };

        cmd.SetHandler((config, id, text, help, page, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new McmService(logger);

            var loadResult = service.Load(config);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mcmConfig = loadResult.Value!;
            var addResult = service.AddToggle(mcmConfig, id, text, help, page);
            if (!addResult.Success)
            {
                OutputError(addResult.Error!, json);
                return;
            }

            var saveResult = service.Save(mcmConfig, config);

            if (json)
            {
                Console.WriteLine(new { success = true, result = new { id, text } }.ToJson());
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Added toggle: {id}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        }, configArg, idArg, textArg, helpOption, pageOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddSliderCommand()
    {
        var configArg = new Argument<string>("config", "Path to the MCM config file");
        var idArg = new Argument<string>("id", "Control identifier");
        var textArg = new Argument<string>("text", "Display text");
        var minOption = new Option<float>("--min", () => 0, "Minimum value");
        var maxOption = new Option<float>("--max", () => 100, "Maximum value");
        var stepOption = new Option<float>("--step", () => 1, "Step increment");

        var cmd = new Command("add-slider", "Add a slider control to MCM config")
        {
            configArg,
            idArg,
            textArg,
            minOption,
            maxOption,
            stepOption
        };

        cmd.SetHandler((config, id, text, min, max, step, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new McmService(logger);

            var loadResult = service.Load(config);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mcmConfig = loadResult.Value!;
            var addResult = service.AddSlider(mcmConfig, id, text, min, max, step, null, null);
            if (!addResult.Success)
            {
                OutputError(addResult.Error!, json);
                return;
            }

            var saveResult = service.Save(mcmConfig, config);

            if (json)
            {
                Console.WriteLine(new { success = true, result = new { id, text, min, max, step } }.ToJson());
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Added slider: {id} ({min} - {max})");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        }, configArg, idArg, textArg, minOption, maxOption, stepOption, _jsonOption, _verboseOption);

        return cmd;
    }

    /// <summary>
    /// settings.ini is mandatory for ModSetting-backed menus - see McmService.GenerateSettingsIni.
    /// </summary>
    private static Command CreateGenerateSettingsCommand()
    {
        var configArg = new Argument<string>("config", "Path to the MCM config.json");
        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output path for settings.ini (defaults to alongside config.json, which is where MCM Helper expects it)");

        var cmd = new Command("generate-settings", "Generate the REQUIRED settings.ini defaults file next to config.json")
        {
            configArg,
            outputOption
        };

        cmd.SetHandler((config, output, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new McmService(logger);

            var result = service.GenerateSettingsIni(config, output);
            if (!result.Success)
            {
                OutputError(result.Error!, json);
                return;
            }

            if (json)
            {
                Console.WriteLine(new { success = true, result = new { settingsFile = result.Value } }.ToJson());
            }
            else
            {
                Console.WriteLine($"Generated settings.ini: {result.Value}");
            }
        }, configArg, outputOption, _jsonOption, _verboseOption);

        return cmd;
    }

    /// <summary>
    /// Creates the plugin-side half of an MCM menu. A config.json on its own NEVER produces a
    /// menu: MCM Helper populates the MCM list from quests running its MCM_ConfigBase script,
    /// so the mod's plugin must contain one. Mirrors the structure of known-working config-only
    /// MCM mods (e.g. FasterLoadscreens.esp).
    /// </summary>
    private static Command CreateAddQuestCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file (.esp/.esl)");
        var modNameOption = new Option<string>("--mod-name", "MCM modName - MUST match the MCM/Config/<modName> folder and the config.json modName") { IsRequired = true };
        var editorIdOption = new Option<string?>("--editor-id", "Editor ID for the quest (default: <modName>_ConfigMenuQuest)");
        var noAliasOption = new Option<bool>("--no-player-alias", "Skip the player alias. The alias runs SkyUI's SKI_PlayerLoadGameAlias so the menu re-registers on every load; omit it only if you specifically don't want that.");
        var seqOption = new Option<string?>("--seq-output", "Also write the SEQ file to this directory. Required for the start-game-enabled quest to start in an ALREADY-RUNNING save.");

        var cmd = new Command("add-quest", "Add the MCM_ConfigBase quest a plugin needs for its MCM menu to appear")
        {
            pluginArg, modNameOption, editorIdOption, noAliasOption, seqOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var modName = context.ParseResult.GetValueForOption(modNameOption)!;
            var editorId = context.ParseResult.GetValueForOption(editorIdOption) ?? $"{modName}_ConfigMenuQuest";
            var noAlias = context.ParseResult.GetValueForOption(noAliasOption);
            var seqOutput = context.ParseResult.GetValueForOption(seqOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new SpookysAutomod.Esp.Services.PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }
            var mod = loadResult.Value!;

            if (mod.Quests.Any(q => q.EditorID == editorId))
            {
                OutputError($"Quest already exists: {editorId}", json);
                return;
            }

            // Start-game-enabled + run-once, priority 0 - matches shipped config-only MCM mods.
            var quest = new SpookysAutomod.Esp.Builders.QuestBuilder(mod, editorId)
                .WithName(modName)
                .StartEnabled()
                .RunOnce()
                .WithPriority(0)
                .Build();

            // Attach MCM Helper's own script - this is what puts the mod in the MCM list.
            var adapter = quest.VirtualMachineAdapter as Mutagen.Bethesda.Skyrim.QuestAdapter ?? new Mutagen.Bethesda.Skyrim.QuestAdapter();
            var scriptEntry = new Mutagen.Bethesda.Skyrim.ScriptEntry
            {
                Name = "MCM_ConfigBase",
                Flags = Mutagen.Bethesda.Skyrim.ScriptEntry.Flag.Local
            };
            adapter.Scripts.Add(scriptEntry);
            quest.VirtualMachineAdapter = adapter;

            // MCM Helper matches this property against the MCM/Config/<modName> folder.
            var propService = new SpookysAutomod.Esp.Services.ScriptPropertyService(logger);
            if (!propService.SetStringProperty(scriptEntry, "ModName", modName))
            {
                OutputError($"Failed to set the ModName property on MCM_ConfigBase.", json);
                return;
            }

            if (!noAlias)
            {
                // Forced-referenced to the player so the alias always fills - an alias with no
                // fill method can stop the quest from starting entirely.
                var aliasResult = service.AddAliasToQuest(quest, "PlayerAlias", "SKI_PlayerLoadGameAlias", null, "000014:Skyrim.esm");
                if (!aliasResult.Success) { OutputError(aliasResult.Error!, json); return; }
            }

            var saveResult = service.SavePlugin(mod, plugin);
            if (!saveResult.Success) { OutputError(saveResult.Error!, json); return; }

            string? seqPath = null;
            if (!string.IsNullOrWhiteSpace(seqOutput))
            {
                var seqResult = service.GenerateSeqFile(plugin, seqOutput);
                if (!seqResult.Success) { OutputError(seqResult.Error!, json); return; }
                seqPath = seqResult.Value;
            }

            if (json)
            {
                Console.WriteLine(new
                {
                    success = true,
                    result = new { plugin, quest = editorId, modName, playerAlias = !noAlias, seqFile = seqPath }
                }.ToJson());
            }
            else
            {
                Console.WriteLine($"Added MCM quest '{editorId}' (ModName='{modName}') to {plugin}");
                if (!noAlias) Console.WriteLine("  + PlayerAlias forced to the player, running SKI_PlayerLoadGameAlias");
                if (seqPath != null) Console.WriteLine($"  + SEQ file: {seqPath}");
                Console.WriteLine($"  Reminder: MCM/Config/{modName}/ needs BOTH config.json and settings.ini (see 'mcm generate-settings').");
            }
        });

        return cmd;
    }

    private static void OutputError(string error, bool json)
    {
        if (json)
        {
            Console.WriteLine(Result.Fail(error).ToJson(true));
        }
        else
        {
            Console.Error.WriteLine($"Error: {error}");
        }
        Environment.ExitCode = 1;
    }
}
