using System.CommandLine;
using System.Buffers.Binary;
using System.Text.Json;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Esp.Builders;
using SpookysAutomod.Esp.Services;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace SpookysAutomod.Cli.Commands;

public static class EspCommands
{
    private static Option<bool> _jsonOption = null!;
    private static Option<bool> _verboseOption = null!;

    public static Command Create(Option<bool> jsonOption, Option<bool> verboseOption)
    {
        _jsonOption = jsonOption;
        _verboseOption = verboseOption;

        var espCommand = new Command("esp", "ESP/ESL plugin operations");

        espCommand.AddCommand(CreateCreateCommand());
        espCommand.AddCommand(CreateInfoCommand());
        espCommand.AddCommand(CreateFlagLightCommand());
        espCommand.AddCommand(EspLightCommands.CreateCompactLightCommand(jsonOption));
        espCommand.AddCommand(CreateAddQuestCommand());
        espCommand.AddCommand(CreateAddSpellCommand());
        espCommand.AddCommand(CreateUpdateSpellCommand());
        espCommand.AddCommand(CreateAddGlobalCommand());
        espCommand.AddCommand(CreateAddWeaponCommand());
        espCommand.AddCommand(CreateAddArmorCommand());
        espCommand.AddCommand(CreateAddNpcCommand());
        espCommand.AddCommand(CreateAddBookCommand());
        espCommand.AddCommand(CreateAddPerkCommand());
        espCommand.AddCommand(CreateAddFactionCommand());
        espCommand.AddCommand(CreateAttachScriptCommand());
        espCommand.AddCommand(CreateAddAliasCommand());
        espCommand.AddCommand(CreateAttachAliasScriptCommand());
        espCommand.AddCommand(CreateSetPropertyCommand());
        espCommand.AddCommand(CreateGenerateSeqCommand());
        espCommand.AddCommand(CreateListMastersCommand());
        espCommand.AddCommand(CreateMergeCommand());
        espCommand.AddCommand(CreateDebugTypesCommand());
        espCommand.AddCommand(CreateAutoFillCommand());
        espCommand.AddCommand(CreateAutoFillAllCommand());
        espCommand.AddCommand(CreateAddLeveledItemCommand());
        espCommand.AddCommand(CreateSetArmorAddonModelCommand());
        espCommand.AddCommand(CreateAddKeywordCommand());
        espCommand.AddCommand(CreateAddFormListCommand());
        espCommand.AddCommand(CreateAddEncounterZoneCommand());
        espCommand.AddCommand(CreateAddLocationCommand());
        espCommand.AddCommand(CreateAddOutfitCommand());
        espCommand.AddCommand(CreateViewRecordCommand());
        espCommand.AddCommand(CreateOverrideCommand());
        espCommand.AddCommand(CreateFindRecordCommand());
        espCommand.AddCommand(CreateListRecordsCommand());
        espCommand.AddCommand(CreateBatchOverrideCommand());
        espCommand.AddCommand(CreateBatchRenameCommand());
        espCommand.AddCommand(CreateCompareRecordCommand());
        espCommand.AddCommand(CreateConflictsCommand());
        espCommand.AddCommand(CreateListConditionsCommand());
        espCommand.AddCommand(CreateRemoveConditionCommand());
        espCommand.AddCommand(CreateAddConditionCommand());
        espCommand.AddCommand(CreateAddPackageCommand());
        espCommand.AddCommand(CreateAttachPackageCommand());
        espCommand.AddCommand(CreateRemoveRecordCommand());
        espCommand.AddCommand(CreateCloneRecordCommand());
        espCommand.AddCommand(CreateAddPlacedObjectCommand());
        espCommand.AddCommand(CreateOverridePlacedObjectCommand());
        espCommand.AddCommand(CreateRemovePlacedObjectCommand());
        espCommand.AddCommand(CreateAddRecipeCommand());
        espCommand.AddCommand(CreateRemoveKeywordCommand());
        espCommand.AddCommand(CreateClearArmorEnchantmentCommand());
        espCommand.AddCommand(CreateSetArmorNameCommand());
        espCommand.AddCommand(CreateImportArmorCommand());
        espCommand.AddCommand(CreateRemoveMasterCommand());
        espCommand.AddCommand(CreateAddContainerCommand());
        espCommand.AddCommand(CreateCreateCellCommand());
        espCommand.AddCommand(CreateAddPlacedObjectLocalCommand());
        espCommand.AddCommand(CreateClearContainerItemsLocalCommand());
        espCommand.AddCommand(CreateAddContainerItemLocalCommand());
        espCommand.AddCommand(CreateRemoveScriptCommand());

        return espCommand;
    }

    private static IModLogger CreateLogger(bool json, bool verbose) =>
        json ? new SilentLogger() : new ConsoleLogger(verbose);

    private static Command CreateCreateCommand()
    {
        var nameArg = new Argument<string>("name", "Name of the plugin file (e.g., MyMod.esp)");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => ".",
            description: "Output directory");
        var lightOption = new Option<bool>("--light", "Create as a light plugin (ESL flagged)");
        var authorOption = new Option<string?>("--author", "Author name for the plugin header");
        var descOption = new Option<string?>("--description", "Description for the plugin header");

        var cmd = new Command("create", "Create a new ESP/ESL plugin")
        {
            nameArg,
            outputOption,
            lightOption,
            authorOption,
            descOption
        };

        cmd.SetHandler((name, output, light, author, desc, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.CreatePlugin(name, output, light, author, desc);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Created plugin: {result.Value}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, nameArg, outputOption, lightOption, authorOption, descOption,
           _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateInfoCommand()
    {
        var pathArg = new Argument<string>("plugin", "Path to the plugin file");

        var cmd = new Command("info", "Get information about a plugin")
        {
            pathArg
        };

        cmd.SetHandler((path, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.GetPluginInfo(path);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var info = result.Value;
                Console.WriteLine($"Plugin: {info.FileName}");
                Console.WriteLine($"Path: {info.FilePath}");
                Console.WriteLine($"Size: {info.FileSize:N0} bytes");
                Console.WriteLine($"Light: {info.IsLight}");
                Console.WriteLine($"Master: {info.IsMaster}");

                if (!string.IsNullOrEmpty(info.Author))
                    Console.WriteLine($"Author: {info.Author}");

                if (info.MasterFiles.Count > 0)
                {
                    Console.WriteLine($"Masters: {string.Join(", ", info.MasterFiles)}");
                }

                Console.WriteLine($"\nRecords ({info.TotalRecords} total):");
                foreach (var (type, count) in info.RecordCounts.Where(kv => kv.Value > 0))
                {
                    Console.WriteLine($"  {type}: {count}");
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                Environment.ExitCode = 1;
            }
        }, pathArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateFlagLightCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the existing ESP plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output path for the flagged plugin (must keep the source filename)")
        {
            IsRequired = true
        };

        var cmd = new Command("flag-light", "Set the ESL flag on an existing ESP without rewriting its records")
        {
            pluginArg,
            outputOption
        };

        cmd.SetHandler((pluginPath, outputPath, json) =>
        {
            try
            {
                var sourcePath = Path.GetFullPath(pluginPath);
                var destinationPath = Path.GetFullPath(outputPath);
                if (!string.Equals(Path.GetExtension(sourcePath), ".esp", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The source must be an .esp plugin.");
                if (!string.Equals(Path.GetExtension(destinationPath), ".esp", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The output must use the .esp extension.");
                if (!string.Equals(Path.GetFileName(sourcePath), Path.GetFileName(destinationPath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The output must keep the source filename so the plugin identity stays unchanged.");
                if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Source and output paths must differ; the source is never overwritten.");
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("Source plugin was not found.", sourcePath);
                if (File.Exists(destinationPath))
                    throw new IOException("Output already exists; choose a new staging path.");

                var sourceMod = SkyrimMod.CreateFromBinary(sourcePath, SkyrimRelease.SkyrimSE);
                var sourceRecords = sourceMod.EnumerateMajorRecords().ToArray();
                var localRecords = sourceRecords.Where(r => r.FormKey.ModKey == sourceMod.ModKey).ToArray();
                var outOfRange = localRecords.Where(r => r.FormKey.ID > 0xFFF).Select(r => r.FormKey.ToString()).ToArray();
                if (outOfRange.Length > 0)
                    throw new InvalidOperationException($"Plugin-owned FormIDs exceed the ESL range 0x000-0xFFF: {string.Join(", ", outOfRange)}. Use esp compact-light to stage a remapped ESL plugin; changed FormIDs can affect saves and dependent patches.");

                var sourceBytes = File.ReadAllBytes(sourcePath);
                if (sourceBytes.Length < 12 || !sourceBytes.AsSpan(0, 4).SequenceEqual("TES4"u8))
                    throw new InvalidDataException("The plugin does not begin with a valid TES4 record header.");

                const int recordFlagsOffset = 8;
                const uint smallFlag = 0x00000200;
                var originalFlags = BinaryPrimitives.ReadUInt32LittleEndian(sourceBytes.AsSpan(recordFlagsOffset, 4));
                var updatedFlags = originalFlags | smallFlag;
                var outputData = sourceBytes.ToArray();
                BinaryPrimitives.WriteUInt32LittleEndian(outputData.AsSpan(recordFlagsOffset, 4), updatedFlags);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.WriteAllBytes(destinationPath, outputData);
                var writtenBytes = File.ReadAllBytes(destinationPath);
                var changedOffsets = Enumerable.Range(0, sourceBytes.Length)
                    .Where(i => sourceBytes[i] != writtenBytes[i])
                    .ToArray();
                if (writtenBytes.Length != sourceBytes.Length || changedOffsets.Any(i => i < recordFlagsOffset || i >= recordFlagsOffset + 4))
                    throw new InvalidDataException("Output verification failed: bytes outside the TES4 record flags changed.");
                if (BinaryPrimitives.ReadUInt32LittleEndian(writtenBytes.AsSpan(recordFlagsOffset, 4)) != updatedFlags)
                    throw new InvalidDataException("Output verification failed: TES4 header flags do not match the requested value.");

                var outputMod = SkyrimMod.CreateFromBinary(destinationPath, SkyrimRelease.SkyrimSE);
                var outputRecords = outputMod.EnumerateMajorRecords().ToArray();
                var outputLocalRecords = outputRecords.Where(r => r.FormKey.ModKey == outputMod.ModKey).ToArray();
                var sourceKeys = sourceRecords.Select(r => r.FormKey.ToString()).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                var outputKeys = outputRecords.Select(r => r.FormKey.ToString()).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                var sourceMasters = sourceMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.ToString()).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                var outputMasters = outputMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.ToString()).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                if ((outputMod.ModHeader.Flags & SkyrimModHeader.HeaderFlag.Small) == 0 ||
                    !sourceKeys.SequenceEqual(outputKeys) ||
                    !sourceMasters.SequenceEqual(outputMasters) ||
                    sourceRecords.Length != outputRecords.Length ||
                    outputLocalRecords.Any(r => r.FormKey.ID > 0xFFF))
                    throw new InvalidDataException("Output verification failed: ESL flag, master list, or record identities changed unexpectedly.");

                var result = new
                {
                    success = true,
                    source = sourcePath,
                    output = destinationPath,
                    isLight = true,
                    masterFiles = outputMasters,
                    totalMajorRecords = outputRecords.Length,
                    localRecords = outputLocalRecords.Length,
                    localFormIdMax = outputLocalRecords.Length == 0 ? (uint?)null : outputLocalRecords.Max(r => r.FormKey.ID),
                    changedByteOffsets = changedOffsets
                };
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                else
                    Console.WriteLine($"Flagged {Path.GetFileName(destinationPath)} as ESL. {outputLocalRecords.Length} local records; masters preserved; changed bytes: {string.Join(", ", changedOffsets)}.");
            }
            catch (Exception ex)
            {
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }, new JsonSerializerOptions { WriteIndented = true }));
                else
                    Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, pluginArg, outputOption, _jsonOption);

        return cmd;
    }
    private static Command CreateAddQuestCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the quest");
        var nameOption = new Option<string?>("--name", "Display name for the quest");
        var startEnabledOption = new Option<bool>("--start-enabled", "Quest starts when game loads");
        var runOnceOption = new Option<bool>("--run-once", "Quest runs only once");
        var priorityOption = new Option<byte>("--priority", getDefaultValue: () => 50, description: "Quest priority (0-255)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-quest", "Add a quest record to a plugin")
        {
            pluginArg,
            editorIdArg,
            nameOption,
            startEnabledOption,
            runOnceOption,
            priorityOption,
            dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var startEnabled = context.ParseResult.GetValueForOption(startEnabledOption);
            var runOnce = context.ParseResult.GetValueForOption(runOnceOption);
            var priority = context.ParseResult.GetValueForOption(priorityOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mod = loadResult.Value!;
            var builder = new QuestBuilder(mod, editorId);

            if (!string.IsNullOrEmpty(name))
                builder.WithName(name);

            if (startEnabled)
                builder.StartEnabled();

            if (runOnce)
                builder.RunOnce();

            builder.WithPriority(priority);

            var quest = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = quest.EditorID,
                            formId = quest.FormKey.ToString(),
                            name = quest.Name?.String,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added quest: {quest.EditorID} ({quest.FormKey})";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddSpellCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the spell");
        var nameOption = new Option<string?>("--name", "Display name for the spell");
        var typeOption = new Option<string>("--type", getDefaultValue: () => "spell",
            description: "Spell type: spell, power, lesser-power, ability");
        var costOption = new Option<uint>("--cost", getDefaultValue: () => 0, description: "Base magicka cost");
        var effectOption = new Option<string?>("--effect",
            description: "Effect preset: damage-health, restore-health, damage-magicka, restore-magicka, damage-stamina, restore-stamina, fortify-health, fortify-magicka, fortify-stamina, fortify-armor, fortify-attack");
        var magnitudeOption = new Option<float>("--magnitude", getDefaultValue: () => 25, description: "Effect magnitude (damage/heal amount or buff value)");
        var durationOption = new Option<int>("--duration", getDefaultValue: () => 0, description: "Effect duration in seconds (0 = instant)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-spell", "Add a spell record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, typeOption, costOption, effectOption, magnitudeOption, durationOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var type = context.ParseResult.GetValueForOption(typeOption) ?? "spell";
            var cost = context.ParseResult.GetValueForOption(costOption);
            var effect = context.ParseResult.GetValueForOption(effectOption);
            var magnitude = context.ParseResult.GetValueForOption(magnitudeOption);
            var duration = context.ParseResult.GetValueForOption(durationOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mod = loadResult.Value!;
            var builder = new SpellBuilder(mod, editorId);

            if (!string.IsNullOrEmpty(name))
                builder.WithName(name);

            builder.WithBaseCost(cost);

            switch (type.ToLowerInvariant())
            {
                case "power":
                    builder.AsGreaterPower();
                    break;
                case "lesser-power":
                    builder.AsLesserPower();
                    break;
                case "ability":
                    builder.AsAbility();
                    break;
                default:
                    builder.WithType(SpellType.Spell);
                    break;
            }

            // Apply effect if specified
            bool hasEffect = false;
            if (!string.IsNullOrEmpty(effect))
            {
                hasEffect = true;
                switch (effect.ToLowerInvariant())
                {
                    case "damage-health": builder.WithDamageHealth(magnitude, duration); break;
                    case "restore-health": builder.WithRestoreHealth(magnitude, duration); break;
                    case "damage-magicka": builder.WithDamageMagicka(magnitude, duration); break;
                    case "restore-magicka": builder.WithRestoreMagicka(magnitude, duration); break;
                    case "damage-stamina": builder.WithDamageStamina(magnitude, duration); break;
                    case "restore-stamina": builder.WithRestoreStamina(magnitude, duration); break;
                    case "fortify-health": builder.WithFortifyHealth(magnitude, duration > 0 ? duration : 60); break;
                    case "fortify-magicka": builder.WithFortifyMagicka(magnitude, duration > 0 ? duration : 60); break;
                    case "fortify-stamina": builder.WithFortifyStamina(magnitude, duration > 0 ? duration : 60); break;
                    case "fortify-armor": builder.WithFortifyArmor(magnitude, duration > 0 ? duration : 60); break;
                    case "fortify-attack": builder.WithFortifyAttackDamage(magnitude / 100f, duration > 0 ? duration : 60); break;
                    default:
                        hasEffect = false;
                        break;
                }
            }

            var spell = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            var effectCount = spell.Effects.Count;
            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = spell.EditorID,
                            formId = spell.FormKey.ToString(),
                            name = spell.Name?.String,
                            effectCount,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added spell: {spell.EditorID} ({spell.FormKey})";
                if (hasEffect)
                    msg += $" [{effectCount} effect(s)]";
                else
                    msg += " [No effects - spell will do nothing! Use --effect to add one]";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateUpdateSpellCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin containing the spell");
        var outputOption = new Option<string>(new[] { "--output", "-o" }, "Output path for the new override plugin (relative to the current directory if not rooted)")
        {
            IsRequired = true
        };
        var editorIdOption = new Option<string>("--editor-id", "EditorID of the spell to update")
        {
            IsRequired = true
        };
        var typeOption = new Option<string?>("--type", "New type: spell, power, lesser-power, or ability");
        var equipmentTypeOption = new Option<string?>("--equipment-type", "Equipment type FormKey (FORMID:MODNAME)");

        var cmd = new Command("update-spell", "Create an override patch changing fields on an existing spell")
        {
            sourceArg,
            outputOption,
            editorIdOption,
            typeOption,
            equipmentTypeOption
        };

        cmd.SetHandler(context =>
        {
            var source = context.ParseResult.GetValueForArgument(sourceArg);
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var editorId = context.ParseResult.GetValueForOption(editorIdOption)!;
            var typeText = context.ParseResult.GetValueForOption(typeOption);
            var equipmentTypeText = context.ParseResult.GetValueForOption(equipmentTypeOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            if (string.IsNullOrWhiteSpace(typeText) && string.IsNullOrWhiteSpace(equipmentTypeText))
            {
                OutputError("Specify at least one of --type or --equipment-type.", json);
                return;
            }

            SpellType? spellType = null;
            if (!string.IsNullOrWhiteSpace(typeText))
            {
                spellType = typeText.Trim().ToLowerInvariant() switch
                {
                    "spell" => SpellType.Spell,
                    "power" => SpellType.Power,
                    "lesser-power" => SpellType.LesserPower,
                    "ability" => SpellType.Ability,
                    _ => null
                };
                if (!spellType.HasValue)
                {
                    OutputError($"Unknown spell type '{typeText}'. Use spell, power, lesser-power, or ability.", json);
                    return;
                }
            }

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);
            var result = service.UpdateSpell(source, output, editorId, spellType, equipmentTypeText);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Created ESL spell override patch: {result.Value}");
            }
            else
            {
                OutputError(result.Error ?? "Failed to update spell.", json, result.Suggestions);
            }
        });

        return cmd;
    }

    private static Command CreateAddGlobalCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the global");
        var typeOption = new Option<string>("--type", getDefaultValue: () => "float",
            description: "Global type: short, long, float");
        var valueOption = new Option<float>("--value", getDefaultValue: () => 0, description: "Initial value");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-global", "Add a global variable to a plugin")
        {
            pluginArg,
            editorIdArg,
            typeOption,
            valueOption,
            dryRunOption
        };

        cmd.SetHandler((plugin, editorId, type, value, dryRun, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mod = loadResult.Value!;
            var builder = new GlobalBuilder(mod, editorId);

            switch (type.ToLowerInvariant())
            {
                case "short":
                    builder.AsShort((short)value);
                    break;
                case "long":
                    builder.AsLong((int)value);
                    break;
                default:
                    builder.AsFloat(value);
                    break;
            }

            var global = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            // Get the value from the concrete global type
            var globalValue = global switch
            {
                GlobalFloat f => f.Data,
                GlobalInt i => i.Data,
                GlobalShort s => s.Data,
                _ => 0f
            };

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = global.EditorID,
                            formId = global.FormKey.ToString(),
                            value = globalValue,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added global: {global.EditorID} ({global.FormKey}) = {globalValue}";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        }, pluginArg, editorIdArg, typeOption, valueOption, dryRunOption,
           _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddFactionCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the faction");
        var nameOption = new Option<string?>("--name", "Display name of the faction");
        var hiddenOption = new Option<bool>("--hidden-from-pc", "Hide faction from player's faction list");
        var trackCrimeOption = new Option<bool>("--track-crime", "Faction tracks crimes against its members");
        var specialCombatOption = new Option<bool>("--special-combat", "Enable special combat behavior");
        var canBeOwnerOption = new Option<bool>("--can-be-owner", "Faction can own items and properties");
        var vendorOption = new Option<bool>("--vendor", "Faction acts as vendor faction");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-faction", "Add a faction record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, hiddenOption, trackCrimeOption,
            specialCombatOption, canBeOwnerOption, vendorOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var hiddenFromPC = context.ParseResult.GetValueForOption(hiddenOption);
            var trackCrime = context.ParseResult.GetValueForOption(trackCrimeOption);
            var specialCombat = context.ParseResult.GetValueForOption(specialCombatOption);
            var canBeOwner = context.ParseResult.GetValueForOption(canBeOwnerOption);
            var vendor = context.ParseResult.GetValueForOption(vendorOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new FactionBuilder(mod, editorId);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);
            if (hiddenFromPC) builder.HiddenFromPC();
            if (trackCrime) builder.TrackCrime();
            if (specialCombat) builder.SpecialCombat();
            if (canBeOwner) builder.CanBeOwner();
            if (vendor) builder.AsVendor();

            var faction = builder.Build();

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = faction.EditorID,
                            formId = faction.FormKey.ToString(),
                            flags = faction.Flags.ToString(),
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added faction: {faction.EditorID} ({faction.FormKey})";
                if (faction.Flags != 0) msg += $" [Flags: {faction.Flags}]";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddContainerCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the container");
        var nameOption = new Option<string?>("--name", "Display name of the container");
        var weightOption = new Option<float?>("--weight", "Carry weight limit (omit for unlimited)");
        var itemOption = new Option<string?>("--item", "Seed the container with starting stock: a FormKey as 'FORMID:PluginName.esp'");
        var countOption = new Option<int>("--count", () => 1, "Count for --item (default 1)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-container", "Add a container record (CONT) to a plugin")
        {
            pluginArg, editorIdArg, nameOption, weightOption, itemOption, countOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var weight = context.ParseResult.GetValueForOption(weightOption);
            var item = context.ParseResult.GetValueForOption(itemOption);
            var count = context.ParseResult.GetValueForOption(countOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new ContainerBuilder(mod, editorId);
            if (!string.IsNullOrEmpty(name)) builder.WithName(name);
            if (weight.HasValue) builder.WithWeight(weight.Value);
            if (!string.IsNullOrEmpty(item))
            {
                if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(item, out var itemKey))
                {
                    OutputError($"Invalid --item FormKey format: {item}", json,
                        suggestions: new[] { "Use format: 000800:PluginName.esp" });
                    return;
                }
                builder.WithItem(itemKey, count);
            }

            var container = builder.Build();

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new { editorId = container.EditorID, formId = container.FormKey.ToString(), dryRun }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added container: {container.EditorID} ({container.FormKey})";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateCreateCellCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the new interior cell");
        var nameOption = new Option<string?>("--name", "Display name of the cell (omit for an unnamed/hidden utility cell)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("create-cell", "Create a standalone interior cell (not attached to any worldspace) - for hosting placed objects that should never be visited, e.g. persistent storage containers")
        {
            pluginArg, editorIdArg, nameOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new CellBuilder(mod, editorId).AsHiddenUtilityCell();
            if (!string.IsNullOrEmpty(name)) builder.WithName(name);

            var cell = builder.Build();

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new { editorId = cell.EditorID, formId = cell.FormKey.ToString(), dryRun }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Created cell: {cell.EditorID} ({cell.FormKey})";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddPlacedObjectLocalCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var cellOption = new Option<string>("--cell", "Editor ID of a cell that was created IN THIS SAME plugin (e.g. via 'esp create-cell')") { IsRequired = true };
        var baseOption = new Option<string>("--base", "FormKey of the base object to place, as 'FORMID:PluginName.esp' e.g. '000808:MyMod.esp'") { IsRequired = true };
        var posOption = new Option<string>("--pos", "Position as x,y,z") { IsRequired = true };
        var rotOption = new Option<string>("--rot", "Rotation in degrees as x,y,z (default 0,0,0)");
        var scaleOption = new Option<float>("--scale", () => 1.0f, "Scale (default 1.0)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-placed-object-local", "Add a placed object reference into a cell owned by THIS SAME plugin - simpler and more reliable than 'add-placed-object' for that case, no separate patch/load-order needed")
        {
            pluginArg, cellOption, baseOption, posOption, rotOption, scaleOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var cellEditorId = context.ParseResult.GetValueForOption(cellOption)!;
            var baseObj = context.ParseResult.GetValueForOption(baseOption)!;
            var pos = context.ParseResult.GetValueForOption(posOption)!;
            var rot = context.ParseResult.GetValueForOption(rotOption);
            var scale = context.ParseResult.GetValueForOption(scaleOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            static bool TryParseTriple(string? s, out float x, out float y, out float z)
            {
                x = y = z = 0f;
                if (string.IsNullOrWhiteSpace(s)) return true;
                var parts = s.Split(',');
                if (parts.Length != 3) return false;
                return float.TryParse(parts[0].Trim(), out x)
                    && float.TryParse(parts[1].Trim(), out y)
                    && float.TryParse(parts[2].Trim(), out z);
            }

            if (!TryParseTriple(pos, out var px, out var py, out var pz))
            {
                OutputError("Invalid --pos, expected 'x,y,z'", json);
                return;
            }
            if (!TryParseTriple(rot, out var rx, out var ry, out var rz))
            {
                OutputError("Invalid --rot, expected 'x,y,z'", json);
                return;
            }
            if (!FormKey.TryFactory(baseObj, out var baseKey))
            {
                OutputError($"Invalid base object FormKey format: {baseObj}", json,
                    suggestions: new[] { "Use format: 000800:PluginName.esp" });
                return;
            }

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }
            var mod = loadResult.Value!;

            var addResult = service.AddPlacedObjectToLocalCell(mod, cellEditorId, baseKey, px, py, pz, rx, ry, rz, scale);
            if (!addResult.Success) { OutputError(addResult.Error!, json); return; }

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new { success = true, result = new { formId = addResult.Value, cell = cellEditorId, dryRun } }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added placed object {addResult.Value} into cell '{cellEditorId}'";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateClearContainerItemsLocalCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var containerOption = new Option<string>("--container", "Editor ID of an existing container in THIS plugin") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("clear-container-items-local",
            "Remove ALL starting-stock items from a container in this same plugin, so it can be re-seeded " +
            "cleanly with add-container-item-local. There is no per-item remove - use this and re-add.")
        {
            pluginArg, containerOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var containerEditorId = context.ParseResult.GetValueForOption(containerOption)!;
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }
            var mod = loadResult.Value!;

            var clearResult = service.ClearLocalContainerItems(mod, containerEditorId);
            if (!clearResult.Success) { OutputError(clearResult.Error!, json); return; }

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                Console.WriteLine(saveResult.Success
                    ? (object)new { success = true, result = new { container = containerEditorId, detail = clearResult.Value, dryRun } }.ToJson()
                    : new { success = false, error = saveResult.Error }.ToJson());
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Cleared container '{containerEditorId}': {clearResult.Value}" + (dryRun ? " [DRY RUN - not saved]" : ""));
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }

            if (!saveResult.Success) { Environment.ExitCode = 1; }
        });

        return cmd;
    }

    private static Command CreateAddContainerItemLocalCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var containerOption = new Option<string>("--container", "Editor ID of an existing container in THIS plugin") { IsRequired = true };
        var itemOption = new Option<string>("--item", "FormKey of the item to add, as 'FORMID:PluginName.esp'") { IsRequired = true };
        var countOption = new Option<int>("--count", () => 1, "Count to add (default 1)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-container-item-local", "Add starting stock to an existing container record in this same plugin")
        {
            pluginArg, containerOption, itemOption, countOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var containerEditorId = context.ParseResult.GetValueForOption(containerOption)!;
            var item = context.ParseResult.GetValueForOption(itemOption)!;
            var count = context.ParseResult.GetValueForOption(countOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            if (!FormKey.TryFactory(item, out var itemKey))
            {
                OutputError($"Invalid --item FormKey format: {item}", json,
                    suggestions: new[] { "Use format: 000800:PluginName.esp" });
                return;
            }

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }
            var mod = loadResult.Value!;

            var addResult = service.AddItemToLocalContainer(mod, containerEditorId, itemKey, count);
            if (!addResult.Success) { OutputError(addResult.Error!, json); return; }

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new { success = true, result = new { container = containerEditorId, item, count, dryRun } }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added {count}x {item} to container '{containerEditorId}'";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAttachScriptCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var questOption = new Option<string>("--quest", "Editor ID of the quest to attach to") { IsRequired = true };
        var scriptOption = new Option<string>("--script", "Name of the script to attach") { IsRequired = true };

        var cmd = new Command("attach-script", "Attach a script to a quest")
        {
            pluginArg,
            questOption,
            scriptOption
        };

        cmd.SetHandler((plugin, questId, scriptName, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mod = loadResult.Value!;

            // Find the quest
            var quest = mod.Quests.FirstOrDefault(q => q.EditorID == questId);
            if (quest == null)
            {
                OutputError($"Quest not found: {questId}", json,
                    suggestions: new[] { "Check the quest editor ID is correct", "Use 'esp info' to list quests in the plugin" });
                return;
            }

            // Get or create adapter
            var adapter = quest.VirtualMachineAdapter as QuestAdapter ?? new QuestAdapter();

            // Check if script already attached
            if (adapter.Scripts.Any(s => s.Name == scriptName))
            {
                OutputError($"Script already attached: {scriptName}", json);
                return;
            }

            // Add script
            var scriptEntry = new ScriptEntry
            {
                Name = scriptName,
                Flags = ScriptEntry.Flag.Local
            };
            adapter.Scripts.Add(scriptEntry);
            quest.VirtualMachineAdapter = adapter;

            var saveResult = service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            quest = questId,
                            script = scriptName
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Attached script '{scriptName}' to quest '{questId}'");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        }, pluginArg, questOption, scriptOption,
           _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddAliasCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var questOption = new Option<string>("--quest", "Editor ID of the quest") { IsRequired = true };
        var nameOption = new Option<string>("--name", "Name of the alias to add") { IsRequired = true };
        var scriptOption = new Option<string?>("--script", "Optional script to attach to the new alias");
        var flagsOption = new Option<string?>("--flags", "Comma-separated alias flags (e.g. 'Optional,AllowReuseInQuest,AllowReserved')");
        var forcedRefOption = new Option<string?>("--forced-ref", "Forced reference FormKey to fill this alias with, as 'FORMID:MODNAME' (e.g. '000014:Skyrim.esm' for the player). Without a fill method an alias can fail to fill, which can prevent the quest from starting.");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-alias", "Add a reference alias to a quest, optionally with a script attached")
        {
            pluginArg, questOption, nameOption, scriptOption, flagsOption, forcedRefOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var questId = context.ParseResult.GetValueForOption(questOption)!;
            var aliasName = context.ParseResult.GetValueForOption(nameOption)!;
            var scriptName = context.ParseResult.GetValueForOption(scriptOption);
            var flagsStr = context.ParseResult.GetValueForOption(flagsOption);
            var forcedRef = context.ParseResult.GetValueForOption(forcedRefOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            // Parse flags
            QuestAlias.Flag? flags = null;
            if (!string.IsNullOrWhiteSpace(flagsStr))
            {
                if (!Enum.TryParse<QuestAlias.Flag>(flagsStr, ignoreCase: true, out var parsed))
                {
                    OutputError($"Invalid alias flags: '{flagsStr}'", json,
                        suggestions: new[] { "Valid flags: Optional, AllowReuseInQuest, AllowReserved, Essential, Protected, StoresText, AllowDeadActor, ClearsNameWhenRemoved, etc.", "Combine flags with commas: --flags 'Optional,AllowReuseInQuest'" });
                    return;
                }
                flags = parsed;
            }

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var quest = mod.Quests.FirstOrDefault(q => q.EditorID == questId);
            if (quest == null)
            {
                OutputError($"Quest not found: {questId}", json,
                    suggestions: new[] { "Check the quest editor ID is correct", "Use 'esp info' to list quests in the plugin" });
                return;
            }

            var addResult = service.AddAliasToQuest(quest, aliasName, scriptName, flags, forcedRef);
            if (!addResult.Success) { OutputError(addResult.Error!, json); return; }
            var alias = addResult.Value!;

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            quest = questId,
                            alias = alias.Name,
                            aliasId = alias.ID,
                            script = scriptName,
                            flags = alias.Flags?.ToString(),
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added alias '{alias.Name}' (ID {alias.ID}) to quest '{questId}'";
                if (!string.IsNullOrEmpty(scriptName)) msg += $" with script '{scriptName}'";
                if (alias.Flags != null) msg += $" [Flags: {alias.Flags}]";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAttachAliasScriptCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var questOption = new Option<string>("--quest", "Editor ID of the quest") { IsRequired = true };
        var aliasOption = new Option<string>("--alias", "Name of the alias to attach the script to") { IsRequired = true };
        var scriptOption = new Option<string>("--script", "Name of the script to attach") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("attach-alias-script", "Attach a script to an existing quest alias")
        {
            pluginArg, questOption, aliasOption, scriptOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var questId = context.ParseResult.GetValueForOption(questOption)!;
            var aliasName = context.ParseResult.GetValueForOption(aliasOption)!;
            var scriptName = context.ParseResult.GetValueForOption(scriptOption)!;
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var quest = mod.Quests.FirstOrDefault(q => q.EditorID == questId);
            if (quest == null)
            {
                OutputError($"Quest not found: {questId}", json,
                    suggestions: new[] { "Check the quest editor ID is correct", "Use 'esp info' to list quests in the plugin" });
                return;
            }

            var attachResult = service.AttachScriptToAliasByName(quest, aliasName, scriptName);
            if (!attachResult.Success)
            {
                OutputError(attachResult.Error!, json,
                    suggestions: new[] { "Use 'esp add-alias' to create the alias first", "Alias names are case-sensitive" });
                return;
            }

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            quest = questId,
                            alias = aliasName,
                            script = scriptName,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Attached script '{scriptName}' to alias '{aliasName}' on quest '{questId}'";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateSetPropertyCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var questOption = new Option<string>("--quest", "Editor ID of the quest") { IsRequired = true };
        var scriptOption = new Option<string>("--script", "Name of the script") { IsRequired = true };
        var propertyOption = new Option<string>("--property", "Property name") { IsRequired = true };
        var typeOption = new Option<string>("--type", "Property type: object, alias, int, float, bool, string") { IsRequired = true };
        var valueOption = new Option<string>("--value", "Property value (for object: 'Plugin.esp|0xFormID', for alias: alias name)") { IsRequired = true };
        var aliasTargetOption = new Option<string?>("--alias-target", "Target alias name (for setting properties on alias scripts instead of quest scripts)");

        var cmd = new Command("set-property", "Set a script property on a quest or alias script")
        {
            pluginArg, questOption, scriptOption, propertyOption, typeOption, valueOption, aliasTargetOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var questId = context.ParseResult.GetValueForOption(questOption)!;
            var scriptName = context.ParseResult.GetValueForOption(scriptOption)!;
            var propertyName = context.ParseResult.GetValueForOption(propertyOption)!;
            var propType = context.ParseResult.GetValueForOption(typeOption)!;
            var value = context.ParseResult.GetValueForOption(valueOption)!;
            var aliasTarget = context.ParseResult.GetValueForOption(aliasTargetOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var pluginService = new PluginService(logger);
            var propService = new ScriptPropertyService(logger);

            var loadResult = pluginService.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json);
                return;
            }

            var mod = loadResult.Value!;

            // Find the quest
            var quest = mod.Quests.FirstOrDefault(q => q.EditorID == questId);
            if (quest == null)
            {
                OutputError($"Quest not found: {questId}", json);
                return;
            }

            // Find the script (on quest or on alias)
            ScriptEntry? script = null;
            if (!string.IsNullOrEmpty(aliasTarget))
            {
                // Find script on alias (via QuestFragmentAlias)
                var alias = quest.Aliases.FirstOrDefault(a => a.Name == aliasTarget);
                if (alias == null)
                {
                    OutputError($"Alias not found: {aliasTarget}", json);
                    return;
                }
                script = propService.FindAliasScript(quest, aliasTarget, scriptName);
                if (script == null)
                {
                    OutputError($"Script '{scriptName}' not found on alias '{aliasTarget}'", json,
                        suggestions: new[] {
                            "Attach the script first with 'esp attach-script' or by creating the alias with a script",
                            "Note: Alias scripts are stored in QuestFragmentAlias within the Quest's VirtualMachineAdapter"
                        });
                    return;
                }
            }
            else
            {
                // Find script on quest
                script = propService.FindQuestScript(quest, scriptName);
                if (script == null)
                {
                    OutputError($"Script '{scriptName}' not found on quest '{questId}'", json,
                        suggestions: new[] { "Attach the script first with 'esp attach-script'" });
                    return;
                }
            }

            // Set the property based on type
            bool success = propType.ToLowerInvariant() switch
            {
                "object" => propService.SetObjectProperty(script, propertyName, value),
                "alias" => propService.SetAliasProperty(script, propertyName, quest, value),
                "int" => propService.SetIntProperty(script, propertyName, int.Parse(value)),
                "float" => propService.SetFloatProperty(script, propertyName, float.Parse(value)),
                "bool" => propService.SetBoolProperty(script, propertyName, bool.Parse(value)),
                "string" => propService.SetStringProperty(script, propertyName, value),
                _ => false
            };

            if (!success)
            {
                OutputError($"Failed to set property '{propertyName}'", json);
                return;
            }

            var saveResult = pluginService.SavePlugin(mod, plugin);

            if (json)
            {
                Console.WriteLine(new
                {
                    success = saveResult.Success,
                    result = new { quest = questId, script = scriptName, property = propertyName, type = propType, value },
                    error = saveResult.Error
                }.ToJson());
            }
            else if (saveResult.Success)
            {
                var target = !string.IsNullOrEmpty(aliasTarget) ? $"alias '{aliasTarget}'" : $"quest '{questId}'";
                Console.WriteLine($"Set {propType} property '{propertyName}' = '{value}' on script '{scriptName}' ({target})");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateGenerateSeqCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => ".",
            description: "Output directory for SEQ file");

        var cmd = new Command("generate-seq", "Generate SEQ file for start-enabled quests")
        {
            pluginArg,
            outputOption
        };

        cmd.SetHandler((plugin, output, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.GenerateSeqFile(plugin, output);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Generated SEQ file: {result.Value}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.Suggestions?.Count > 0)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var suggestion in result.Suggestions)
                        Console.Error.WriteLine($"  - {suggestion}");
                }
                Environment.ExitCode = 1;
            }
        }, pluginArg, outputOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddWeaponCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the weapon");
        var nameOption = new Option<string?>("--name", "Display name for the weapon");
        var descriptionOption = new Option<string?>("--description",
            "Item card description text. Some installed UI mods (e.g. DescriptionFramework) can crash " +
            "when displaying a weapon with no description set - always set this for weapons the player " +
            "will actually see in their inventory.");
        var typeOption = new Option<string>("--type", getDefaultValue: () => "sword",
            description: "Weapon type: sword, greatsword, dagger, waraxe, battleaxe, mace, warhammer, bow, crossbow, staff");
        var damageOption = new Option<ushort>("--damage", getDefaultValue: () => 10, description: "Base damage");
        var valueOption = new Option<uint>("--value", getDefaultValue: () => 100, description: "Base value");
        var weightOption = new Option<float>("--weight", getDefaultValue: () => 5, description: "Weight");
        var modelOption = new Option<string?>("--model",
            description: "Model path relative to Data/Meshes (e.g., Weapons\\Iron\\IronSword.nif). Use 'iron-sword', 'steel-sword', 'iron-dagger', or 'hunting-bow' for vanilla presets.");
        var speedOption = new Option<float?>("--speed", "Attack speed multiplier (vanilla weapons are typically 0.8-1.3)");
        var reachOption = new Option<float?>("--reach", "Attack reach multiplier (vanilla weapons are typically 0.7-1.5)");
        var criticalDamageOption = new Option<ushort?>("--critical-damage", "Bonus damage applied on a critical hit");
        var keywordOption = new Option<string[]?>("--keyword",
            description: "Add a keyword in format: FormId:ModName.esp (no 0x prefix) or EditorId (e.g., 01E713:Skyrim.esm for WeapTypeDagger). Can be used multiple times.");
        var boundsOption = new Option<string?>("--bounds",
            "Object bounds as minX,minY,minZ,maxX,maxY,maxZ (game units). A small non-zero default is " +
            "already applied automatically - only set this if you know the real dimensions from the mesh.");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-weapon", "Add a weapon record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, descriptionOption, typeOption, damageOption, valueOption, weightOption, modelOption,
            speedOption, reachOption, criticalDamageOption, keywordOption, boundsOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var description = context.ParseResult.GetValueForOption(descriptionOption);
            var type = context.ParseResult.GetValueForOption(typeOption) ?? "sword";
            var damage = context.ParseResult.GetValueForOption(damageOption);
            var value = context.ParseResult.GetValueForOption(valueOption);
            var weight = context.ParseResult.GetValueForOption(weightOption);
            var model = context.ParseResult.GetValueForOption(modelOption);
            var speed = context.ParseResult.GetValueForOption(speedOption);
            var reach = context.ParseResult.GetValueForOption(reachOption);
            var criticalDamage = context.ParseResult.GetValueForOption(criticalDamageOption);
            var keywords = context.ParseResult.GetValueForOption(keywordOption);
            var bounds = context.ParseResult.GetValueForOption(boundsOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new WeaponBuilder(mod, editorId)
                .WithDamage(damage)
                .WithValue(value)
                .WithWeight(weight);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);
            if (!string.IsNullOrEmpty(description)) builder.WithDescription(description);
            if (speed.HasValue) builder.WithSpeed(speed.Value);
            if (reach.HasValue) builder.WithReach(reach.Value);
            if (criticalDamage.HasValue) builder.WithCriticalDamage(criticalDamage.Value);

            if (!string.IsNullOrEmpty(bounds))
            {
                var parts = bounds.Split(',');
                if (parts.Length != 6 || !parts.All(p => short.TryParse(p.Trim(), out _)))
                {
                    OutputError($"Invalid --bounds value '{bounds}' - expected minX,minY,minZ,maxX,maxY,maxZ", json);
                    return;
                }
                var nums = parts.Select(p => short.Parse(p.Trim())).ToArray();
                builder.WithBounds(nums[0], nums[1], nums[2], nums[3], nums[4], nums[5]);
            }

            if (keywords != null && keywords.Length > 0)
            {
                foreach (var keywordStr in keywords)
                {
                    if (Mutagen.Bethesda.Plugins.FormKey.TryFactory(keywordStr, out var keywordFormKey))
                    {
                        builder.WithKeyword(keywordFormKey);
                    }
                    else
                    {
                        var record = mod.EnumerateMajorRecords().FirstOrDefault(r => r.EditorID == keywordStr);
                        if (record == null)
                        {
                            OutputError($"Keyword not found: {keywordStr}", json);
                            return;
                        }
                        builder.WithKeyword(record.FormKey);
                    }
                }
            }

            // Apply model if specified
            if (!string.IsNullOrEmpty(model))
            {
                switch (model.ToLowerInvariant())
                {
                    case "iron-sword": builder.WithIronSwordModel(); break;
                    case "steel-sword": builder.WithSteelSwordModel(); break;
                    case "iron-dagger": builder.WithIronDaggerModel(); break;
                    case "hunting-bow": builder.WithHuntingBowModel(); break;
                    default: builder.WithModel(model); break;
                }
            }

            switch (type.ToLowerInvariant())
            {
                case "greatsword": builder.AsGreatsword(); break;
                case "dagger": builder.AsDagger(); break;
                case "waraxe": builder.AsWarAxe(); break;
                case "battleaxe": builder.AsBattleaxe(); break;
                case "mace": builder.AsMace(); break;
                case "warhammer": builder.AsWarhammer(); break;
                case "bow": builder.AsBow(); break;
                case "crossbow": builder.AsCrossbow(); break;
                case "staff": builder.AsStaff(); break;
                default: builder.AsSword(); break;
            }

            var weapon = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = weapon.EditorID,
                            formId = weapon.FormKey.ToString(),
                            name = weapon.Name?.String,
                            model = weapon.Model?.File.DataRelativePath,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added weapon: {weapon.EditorID} ({weapon.FormKey})" + (weapon.Model != null ? $" [Model: {weapon.Model.File}]" : " [No model - weapon will be invisible!]");
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddArmorCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the armor");
        var nameOption = new Option<string?>("--name", "Display name for the armor");
        var typeOption = new Option<string>("--type", getDefaultValue: () => "light",
            description: "Armor type: light, heavy, clothing");
        var slotOption = new Option<string>("--slot", getDefaultValue: () => "body",
            description: "Body slot: head, body, hands, feet, shield");
        var ratingOption = new Option<float>("--rating", getDefaultValue: () => 10, description: "Armor rating");
        var valueOption = new Option<uint>("--value", getDefaultValue: () => 100, description: "Base value");
        var modelOption = new Option<string?>("--model",
            description: "Model path relative to Data/Meshes. Presets: 'iron-cuirass', 'iron-helmet', 'iron-gauntlets', 'iron-boots', 'iron-shield'");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-armor", "Add an armor record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, typeOption, slotOption, ratingOption, valueOption, modelOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var type = context.ParseResult.GetValueForOption(typeOption) ?? "light";
            var slot = context.ParseResult.GetValueForOption(slotOption) ?? "body";
            var rating = context.ParseResult.GetValueForOption(ratingOption);
            var value = context.ParseResult.GetValueForOption(valueOption);
            var model = context.ParseResult.GetValueForOption(modelOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new ArmorBuilder(mod, editorId)
                .WithArmorRating(rating)
                .WithValue(value);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);

            // Apply model if specified
            if (!string.IsNullOrEmpty(model))
            {
                switch (model.ToLowerInvariant())
                {
                    case "iron-cuirass": builder.WithIronCuirassModel(); break;
                    case "iron-helmet": builder.WithIronHelmetModel(); break;
                    case "iron-gauntlets": builder.WithIronGauntletsModel(); break;
                    case "iron-boots": builder.WithIronBootsModel(); break;
                    case "iron-shield": builder.WithIronShieldModel(); break;
                    default: builder.WithWorldModel(model); break;
                }
            }

            switch (type.ToLowerInvariant())
            {
                case "heavy": builder.AsHeavyArmor(); break;
                case "clothing": builder.AsClothing(); break;
                default: builder.AsLightArmor(); break;
            }

            switch (slot.ToLowerInvariant())
            {
                case "head": builder.ForHead(); break;
                case "hands": builder.ForHands(); break;
                case "feet": builder.ForFeet(); break;
                case "shield": builder.ForShield(); break;
                default: builder.ForBody(); break;
            }

            var armor = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            var hasModel = armor.WorldModel?.Male?.Model?.File != null;
            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = armor.EditorID,
                            formId = armor.FormKey.ToString(),
                            name = armor.Name?.String,
                            model = armor.WorldModel?.Male?.Model?.File?.DataRelativePath,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added armor: {armor.EditorID} ({armor.FormKey})" + (hasModel ? $" [Model: {armor.WorldModel?.Male?.Model?.File}]" : " [No model - armor will be invisible!]");
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddRecipeCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the recipe");
        var createdObjectOption = new Option<string>("--created-object", "FormKey of the item this recipe creates (format: 0x000800 or hex:PluginName.esp)") { IsRequired = true };
        var createdCountOption = new Option<ushort>("--created-count", getDefaultValue: () => 1, description: "Quantity created");
        var workbenchOption = new Option<string>("--workbench", "FormKey of the crafting station keyword (e.g. CraftingTanningRack, CraftingSmithingForge)") { IsRequired = true };
        var itemOption = new Option<string>("--item", "FormKey of the required ingredient") { IsRequired = true };
        var itemCountOption = new Option<int>("--item-count", getDefaultValue: () => 1, description: "Quantity of the ingredient required");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-recipe", "Add a crafting recipe (ConstructibleObject) record to a plugin - no perk condition, single ingredient")
        {
            pluginArg, editorIdArg, createdObjectOption, createdCountOption, workbenchOption, itemOption, itemCountOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var createdObjectStr = context.ParseResult.GetValueForOption(createdObjectOption)!;
            var createdCount = context.ParseResult.GetValueForOption(createdCountOption);
            var workbenchStr = context.ParseResult.GetValueForOption(workbenchOption)!;
            var itemStr = context.ParseResult.GetValueForOption(itemOption)!;
            var itemCount = context.ParseResult.GetValueForOption(itemCountOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            if (!FormKey.TryFactory(createdObjectStr, out var createdObjectKey))
            {
                OutputError($"Invalid --created-object FormKey: {createdObjectStr}", json);
                return;
            }
            if (!FormKey.TryFactory(workbenchStr, out var workbenchKey))
            {
                OutputError($"Invalid --workbench FormKey: {workbenchStr}", json);
                return;
            }
            if (!FormKey.TryFactory(itemStr, out var itemKey))
            {
                OutputError($"Invalid --item FormKey: {itemStr}", json);
                return;
            }

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var recipe = new RecipeBuilder(mod, editorId)
                .Creates(createdObjectKey, createdCount)
                .AtWorkbench(workbenchKey)
                .RequiresItem(itemKey, itemCount)
                .Build();

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = recipe.EditorID,
                            formId = recipe.FormKey.ToString(),
                            createdObject = createdObjectKey.ToString(),
                            createdCount,
                            workbench = workbenchKey.ToString(),
                            item = itemKey.ToString(),
                            itemCount,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added recipe: {recipe.EditorID} ({recipe.FormKey}) creates {createdObjectKey} x{createdCount} at {workbenchKey} from {itemKey} x{itemCount}";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateSetArmorNameCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID of the armor record to edit");
        var nameOption = new Option<string>("--name", "New display name (FULL) for the armor") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("set-armor-name", "Set an Armor record's display name (FULL)")
        {
            pluginArg, editorIdArg, nameOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption)!;
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var armor = mod.Armors.FirstOrDefault(a => a.EditorID == editorId);
            if (armor == null)
            {
                OutputError($"No Armor record with EditorID '{editorId}' found in {plugin}", json);
                return;
            }

            var previousName = armor.Name?.String;
            armor.Name = name;

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new { editorId = armor.EditorID, formId = armor.FormKey.ToString(), previousName, name, dryRun }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Renamed {editorId}: '{previousName}' -> '{name}'";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateClearArmorEnchantmentCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID of the armor record to edit");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("clear-armor-enchantment",
            "Remove an Armor record's attached ObjectEffect (enchantment) entirely - clears the armor's " +
            "EITM link and deletes the ObjectEffect and every MagicEffect it references, as long as nothing " +
            "else in the plugin still points at them.")
        {
            pluginArg, editorIdArg, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var armor = mod.Armors.FirstOrDefault(a => a.EditorID == editorId);
            if (armor == null)
            {
                OutputError($"No Armor record with EditorID '{editorId}' found in {plugin}", json);
                return;
            }

            if (armor.ObjectEffect.IsNull)
            {
                OutputError($"Armor '{editorId}' has no attached enchantment - nothing to clear.", json);
                return;
            }

            var enchantment = mod.ObjectEffects.FirstOrDefault(e => e.FormKey == armor.ObjectEffect.FormKey);
            var removedMgefEditorIds = new List<string>();
            armor.ObjectEffect.Clear();

            if (enchantment != null)
            {
                var mgefFormKeys = enchantment.Effects.Select(e => e.BaseEffect.FormKey).Distinct().ToList();
                foreach (var mgefKey in mgefFormKeys)
                {
                    var mgef = mod.MagicEffects.FirstOrDefault(m => m.FormKey == mgefKey);
                    if (mgef != null)
                    {
                        removedMgefEditorIds.Add(mgef.EditorID ?? mgef.FormKey.ToString());
                        mod.MagicEffects.Remove(mgef);
                    }
                }
                mod.ObjectEffects.Remove(enchantment);
            }

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = armor.EditorID,
                            formId = armor.FormKey.ToString(),
                            removedEnchantment = enchantment?.EditorID,
                            removedMagicEffects = removedMgefEditorIds,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Cleared enchantment from {editorId}" +
                    (enchantment != null ? $" - removed {enchantment.EditorID} and {removedMgefEditorIds.Count} magic effect(s)" : "");
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateRemoveKeywordCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID of the armor record to edit");
        var keywordOption = new Option<string>("--keyword", "FormKey of the keyword to remove (format: 0x000800 or hex:PluginName.esp)") { IsRequired = true };
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("remove-keyword", "Remove a keyword FormLink from an Armor record already present in the plugin (e.g. after clone-record, to drop a dependency on the source plugin)")
        {
            pluginArg, editorIdArg, keywordOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var keywordStr = context.ParseResult.GetValueForOption(keywordOption)!;
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            if (!FormKey.TryFactory(keywordStr, out var keywordKey))
            {
                OutputError($"Invalid --keyword FormKey: {keywordStr}", json);
                return;
            }

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var armor = mod.Armors.FirstOrDefault(a => a.EditorID == editorId);
            if (armor == null)
            {
                OutputError($"No Armor record with EditorID '{editorId}' found in {plugin}", json);
                return;
            }

            var removed = armor.Keywords?.RemoveAll(k => k.FormKey == keywordKey) ?? 0;

            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new { editorId = armor.EditorID, formId = armor.FormKey.ToString(), removedCount = removed, dryRun }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Removed {removed} keyword reference(s) matching {keywordKey} from {editorId}";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateImportArmorCommand()
    {
        var sourcePluginArg = new Argument<string>("sourcePlugin", "Path to the plugin that owns the armor");
        var targetPluginArg = new Argument<string>("targetPlugin", "Path to the plugin to import into");
        var sourceEditorIdArg = new Argument<string>("sourceEditorId", "EditorID of the Armor record to import");
        var newEditorIdArg = new Argument<string>("newEditorId", "EditorID for the imported Armor record");

        var cmd = new Command(
            "import-armor",
            "Deep-copy an Armor record (and every ArmorAddon it references via Armature) from another plugin into the target plugin as brand-new, fully self-contained local records - the target no longer needs the source plugin as a master for this armor. World model paths (NIF/texture references) are copied as-is; the actual mesh/texture files must already exist under the target's Data folder.")
        {
            sourcePluginArg, targetPluginArg, sourceEditorIdArg, newEditorIdArg
        };

        cmd.SetHandler((context) =>
        {
            var sourcePlugin = context.ParseResult.GetValueForArgument(sourcePluginArg);
            var targetPlugin = context.ParseResult.GetValueForArgument(targetPluginArg);
            var sourceEditorId = context.ParseResult.GetValueForArgument(sourceEditorIdArg);
            var newEditorId = context.ParseResult.GetValueForArgument(newEditorIdArg);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var sourceLoadResult = service.LoadPluginReadOnly(sourcePlugin);
            if (!sourceLoadResult.Success) { OutputError(sourceLoadResult.Error!, json); return; }
            var sourceMod = sourceLoadResult.Value!;
            var linkCache = sourceMod.ToImmutableLinkCache();

            var sourceArmor = sourceMod.Armors.FirstOrDefault(a => a.EditorID == sourceEditorId);
            if (sourceArmor == null)
            {
                OutputError($"No Armor record with EditorID '{sourceEditorId}' found in {sourcePlugin}", json);
                return;
            }

            var targetLoadResult = service.LoadPluginForEdit(targetPlugin);
            if (!targetLoadResult.Success) { OutputError(targetLoadResult.Error!, json); return; }
            var targetMod = targetLoadResult.Value!;

            void EnsureMaster(ModKey modKey)
            {
                if (modKey == targetMod.ModKey) return;
                if (!targetMod.ModHeader.MasterReferences.Any(m => m.Master == modKey))
                {
                    targetMod.ModHeader.MasterReferences.Add(new MasterReference { Master = modKey });
                }
            }

            // Duplicate every ArmorAddon the source armor references, tracking old -> new FormKey
            // so the duplicated Armor's own Armature list can be repointed at the local copies.
            var armaRemap = new Dictionary<FormKey, FormKey>();
            var importedArmaEditorIds = new List<string>();
            int armaIndex = 0;
            foreach (var armaLink in sourceArmor.Armature)
            {
                if (!linkCache.TryResolve<IArmorAddonGetter>(armaLink.FormKey, out var sourceArma))
                {
                    OutputError($"Could not resolve ArmorAddon {armaLink.FormKey} referenced by {sourceEditorId}'s Armature", json);
                    return;
                }

                var newArma = ArmorAddonMixIn.Duplicate(sourceArma, targetMod.GetNextFormKey(), (Mutagen.Bethesda.Skyrim.ArmorAddon.TranslationMask?)null);
                newArma.EditorID = $"{newEditorId}_AA{(armaIndex == 0 ? "" : armaIndex.ToString())}";
                targetMod.ArmorAddons.Add(newArma);
                armaRemap[armaLink.FormKey] = newArma.FormKey;
                importedArmaEditorIds.Add(newArma.EditorID!);
                armaIndex++;

                foreach (var kw in newArma.Race.FormKey.IsNull ? Array.Empty<ModKey>() : new[] { newArma.Race.FormKey.ModKey })
                {
                    EnsureMaster(kw);
                }
            }

            var newArmor = ArmorMixIn.Duplicate(sourceArmor, targetMod.GetNextFormKey(), (Mutagen.Bethesda.Skyrim.Armor.TranslationMask?)null);
            newArmor.EditorID = newEditorId;
            var remappedArmature = newArmor.Armature
                .Select(link => armaRemap.TryGetValue(link.FormKey, out var remapped)
                    ? new Mutagen.Bethesda.Plugins.FormLink<IArmorAddonGetter>(remapped)
                    : link)
                .ToList();
            newArmor.Armature.Clear();
            newArmor.Armature.AddRange(remappedArmature);

            // The armor's ObjectEffect (EITM - an attached enchantment, e.g. a constant-effect
            // carry-weight bonus) is easy to miss since it doesn't show up in view-record's
            // simplified property dump. Duplicate it too so the armor doesn't silently keep a
            // dependency on the source plugin through this one field.
            string? importedEnchantmentEditorId = null;
            if (!newArmor.ObjectEffect.IsNull)
            {
                if (linkCache.TryResolve<IObjectEffectGetter>(newArmor.ObjectEffect.FormKey, out var sourceEnchantment))
                {
                    var newEnchantment = ObjectEffectMixIn.Duplicate(sourceEnchantment, targetMod.GetNextFormKey(), (Mutagen.Bethesda.Skyrim.ObjectEffect.TranslationMask?)null);
                    newEnchantment.EditorID = $"{newEditorId}_ENCH";

                    // Effect entries can reference a MagicEffect (MGEF) local to the source
                    // plugin too (e.g. a custom "+carry weight" effect) - easy to miss since it's
                    // nested two levels deep from the armor. Duplicate any that belong to the
                    // source plugin specifically; leave vanilla/other-plugin ones as external
                    // links (those plugins are expected to already be present).
                    int mgefIndex = 0;
                    foreach (var effect in newEnchantment.Effects)
                    {
                        if (effect.BaseEffect.IsNull || effect.BaseEffect.FormKey.ModKey != sourceMod.ModKey)
                        {
                            continue;
                        }
                        if (!linkCache.TryResolve<IMagicEffectGetter>(effect.BaseEffect.FormKey, out var sourceMagicEffect))
                        {
                            OutputError($"Could not resolve MagicEffect {effect.BaseEffect.FormKey} referenced by {sourceEditorId}'s enchantment", json);
                            return;
                        }
                        var newMagicEffect = MagicEffectMixIn.Duplicate(sourceMagicEffect, targetMod.GetNextFormKey(), (Mutagen.Bethesda.Skyrim.MagicEffect.TranslationMask?)null);
                        newMagicEffect.EditorID = $"{newEditorId}_MGEF{(mgefIndex == 0 ? "" : mgefIndex.ToString())}";
                        targetMod.MagicEffects.Add(newMagicEffect);
                        effect.BaseEffect.SetTo(newMagicEffect.FormKey);
                        mgefIndex++;
                    }

                    targetMod.ObjectEffects.Add(newEnchantment);
                    newArmor.ObjectEffect.SetTo(newEnchantment.FormKey);
                    importedEnchantmentEditorId = newEnchantment.EditorID;

                    if (!newEnchantment.BaseEnchantment.IsNull)
                    {
                        EnsureMaster(newEnchantment.BaseEnchantment.FormKey.ModKey);
                    }
                }
                else
                {
                    OutputError($"Could not resolve ObjectEffect {newArmor.ObjectEffect.FormKey} referenced by {sourceEditorId}'s enchantment slot", json);
                    return;
                }
            }

            targetMod.Armors.Add(newArmor);

            // Any remaining FormLinks (keywords, race, etc.) still point wherever they originally
            // did - ensure those plugins are declared as masters. Use remove-keyword afterward if
            // you want to drop specific foreign keyword references instead of mastering them.
            foreach (var kwLink in newArmor.Keywords ?? Enumerable.Empty<Mutagen.Bethesda.Plugins.IFormLinkGetter<IKeywordGetter>>())
            {
                EnsureMaster(kwLink.FormKey.ModKey);
            }
            if (!newArmor.Race.IsNull)
            {
                EnsureMaster(newArmor.Race.FormKey.ModKey);
            }

            var saveResult = service.SavePlugin(targetMod, targetPlugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            armorEditorId = newArmor.EditorID,
                            armorFormId = newArmor.FormKey.ToString(),
                            armorAddons = importedArmaEditorIds,
                            enchantment = importedEnchantmentEditorId
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var enchMsg = importedEnchantmentEditorId != null ? $" and enchantment {importedEnchantmentEditorId}" : "";
                Console.WriteLine($"Imported armor: {newArmor.EditorID} ({newArmor.FormKey}) with {importedArmaEditorIds.Count} ArmorAddon(s): {string.Join(", ", importedArmaEditorIds)}{enchMsg}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateRemoveMasterCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var masterArg = new Argument<string>("masterFileName", "Filename of the master to remove (e.g. Dr_Bandolier.esp)");

        var cmd = new Command(
            "remove-master",
            "Remove a MasterReference entry from a plugin's header. Does NOT scan records for remaining FormLinks to that master - only use once you've confirmed (e.g. via remove-keyword) that nothing in the plugin references it anymore, or the file will fail to resolve those references at load time.")
        {
            pluginArg, masterArg
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var masterFileName = context.ParseResult.GetValueForArgument(masterArg);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var masterKey = ModKey.FromFileName(masterFileName);
            var removed = mod.ModHeader.MasterReferences.RemoveAll(m => m.Master == masterKey);

            var saveResult = service.SavePlugin(mod, plugin);

            if (json)
            {
                Console.WriteLine(saveResult.Success
                    ? new { success = true, result = new { removed } }.ToJson()
                    : new { success = false, error = saveResult.Error }.ToJson());
                if (!saveResult.Success) Environment.ExitCode = 1;
            }
            else if (saveResult.Success)
            {
                Console.WriteLine(removed > 0 ? $"Removed master: {masterFileName}" : $"Master {masterFileName} was not present");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddNpcCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the NPC");
        var nameOption = new Option<string?>("--name", "Display name for the NPC");
        var levelOption = new Option<short>("--level", getDefaultValue: () => 1, description: "NPC level");
        var femaleOption = new Option<bool>("--female", "NPC is female");
        var essentialOption = new Option<bool>("--essential", "NPC is essential");
        var uniqueOption = new Option<bool>("--unique", "NPC is unique");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-npc", "Add an NPC record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, levelOption, femaleOption, essentialOption, uniqueOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var level = context.ParseResult.GetValueForOption(levelOption);
            var female = context.ParseResult.GetValueForOption(femaleOption);
            var essential = context.ParseResult.GetValueForOption(essentialOption);
            var unique = context.ParseResult.GetValueForOption(uniqueOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new NpcBuilder(mod, editorId).WithLevel(level);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);
            if (female) builder.AsFemale();
            if (essential) builder.AsEssential();
            if (unique) builder.AsUnique();

            var npc = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = npc.EditorID,
                            formId = npc.FormKey.ToString(),
                            name = npc.Name?.String,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added NPC: {npc.EditorID} ({npc.FormKey})";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddBookCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the book");
        var nameOption = new Option<string?>("--name", "Display name for the book");
        var textOption = new Option<string?>("--text", "Book text content");
        var valueOption = new Option<uint>("--value", getDefaultValue: () => 10, description: "Base value");
        var weightOption = new Option<float>("--weight", getDefaultValue: () => 1, description: "Weight");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-book", "Add a book record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, textOption, valueOption, weightOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var text = context.ParseResult.GetValueForOption(textOption);
            var value = context.ParseResult.GetValueForOption(valueOption);
            var weight = context.ParseResult.GetValueForOption(weightOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new BookBuilder(mod, editorId)
                .WithValue(value)
                .WithWeight(weight);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);
            if (!string.IsNullOrEmpty(text)) builder.WithText(text);

            var book = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = book.EditorID,
                            formId = book.FormKey.ToString(),
                            name = book.Name?.String,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added book: {book.EditorID} ({book.FormKey})";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddPerkCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the perk");
        var nameOption = new Option<string?>("--name", "Display name for the perk");
        var descOption = new Option<string?>("--description", "Perk description");
        var playableOption = new Option<bool>("--playable", "Perk is playable");
        var hiddenOption = new Option<bool>("--hidden", "Perk is hidden");
        var effectOption = new Option<string?>("--effect",
            description: "Effect preset: weapon-damage, damage-reduction, armor, spell-cost, spell-power, spell-duration, sneak-attack, pickpocket, prices");
        var bonusOption = new Option<float>("--bonus", getDefaultValue: () => 25, description: "Bonus percentage (e.g., 25 for +25%)");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-perk", "Add a perk record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, descOption, playableOption, hiddenOption, effectOption, bonusOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var desc = context.ParseResult.GetValueForOption(descOption);
            var playable = context.ParseResult.GetValueForOption(playableOption);
            var hidden = context.ParseResult.GetValueForOption(hiddenOption);
            var effect = context.ParseResult.GetValueForOption(effectOption);
            var bonus = context.ParseResult.GetValueForOption(bonusOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new PerkBuilder(mod, editorId);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);
            if (!string.IsNullOrEmpty(desc)) builder.WithDescription(desc);
            if (playable) builder.AsPlayable();
            if (hidden) builder.AsHidden();

            // Apply effect if specified
            bool hasEffect = false;
            if (!string.IsNullOrEmpty(effect))
            {
                hasEffect = true;
                switch (effect.ToLowerInvariant())
                {
                    case "weapon-damage": builder.WithWeaponDamageBonus(bonus); break;
                    case "damage-reduction": builder.WithDamageReduction(bonus); break;
                    case "armor": builder.WithArmorBonus(bonus); break;
                    case "spell-cost": builder.WithSpellCostReduction(bonus); break;
                    case "spell-power": builder.WithSpellPowerBonus(bonus); break;
                    case "spell-duration": builder.WithSpellDurationBonus(bonus); break;
                    case "sneak-attack": builder.WithSneakAttackBonus(1.0f + bonus / 100f); break;
                    case "pickpocket": builder.WithPickpocketBonus(bonus); break;
                    case "prices": builder.WithBetterPrices(bonus); break;
                    default:
                        hasEffect = false;
                        break;
                }
            }

            var perk = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            var effectCount = perk.Effects.Count;
            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = perk.EditorID,
                            formId = perk.FormKey.ToString(),
                            name = perk.Name?.String,
                            effectCount,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added perk: {perk.EditorID} ({perk.FormKey})";
                if (hasEffect)
                    msg += $" [{effectCount} entry/entries]";
                else
                    msg += " [No entries - perk will do nothing! Use --effect to add one]";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateListMastersCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");

        var cmd = new Command("list-masters", "List master file dependencies")
        {
            pluginArg
        };

        cmd.SetHandler((plugin, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.GetPluginInfo(plugin);

            if (json)
            {
                Console.WriteLine(new { success = result.Success, result = new { masters = result.Value?.MasterFiles }, error = result.Error }.ToJson());
            }
            else if (result.Success && result.Value != null)
            {
                Console.WriteLine($"Master files for {result.Value.FileName}:");
                if (result.Value.MasterFiles.Count == 0)
                    Console.WriteLine("  (none)");
                else
                    foreach (var master in result.Value.MasterFiles)
                        Console.WriteLine($"  - {master}");
            }
            else
            { Console.Error.WriteLine($"Error: {result.Error}"); Environment.ExitCode = 1; }
        }, pluginArg, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateMergeCommand()
    {
        var sourceArg = new Argument<string>("source", "Source plugin to merge from");
        var targetArg = new Argument<string>("target", "Target plugin to merge into");
        var outputOption = new Option<string?>("--output", "Output path (defaults to overwriting target)");

        var cmd = new Command("merge", "Merge records from one plugin into another")
        {
            sourceArg, targetArg, outputOption
        };

        cmd.SetHandler((source, target, output, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var sourceResult = service.LoadPluginReadOnly(source);
            if (!sourceResult.Success) { OutputError(sourceResult.Error!, json); return; }

            var targetResult = service.LoadPluginForEdit(target);
            if (!targetResult.Success) { OutputError(targetResult.Error!, json); return; }

            var sourceMod = sourceResult.Value!;
            var targetMod = targetResult.Value!;

            int recordsCopied = 0;

            // Copy all major record types using DeepCopy
            foreach (var quest in sourceMod.Quests) { targetMod.Quests.Add(quest.DeepCopy()); recordsCopied++; }
            foreach (var spell in sourceMod.Spells) { targetMod.Spells.Add(spell.DeepCopy()); recordsCopied++; }
            foreach (var global in sourceMod.Globals) { targetMod.Globals.Add(global.DeepCopy()); recordsCopied++; }
            foreach (var weapon in sourceMod.Weapons) { targetMod.Weapons.Add(weapon.DeepCopy()); recordsCopied++; }
            foreach (var armor in sourceMod.Armors) { targetMod.Armors.Add(armor.DeepCopy()); recordsCopied++; }
            foreach (var npc in sourceMod.Npcs) { targetMod.Npcs.Add(npc.DeepCopy()); recordsCopied++; }
            foreach (var book in sourceMod.Books) { targetMod.Books.Add(book.DeepCopy()); recordsCopied++; }
            foreach (var perk in sourceMod.Perks) { targetMod.Perks.Add(perk.DeepCopy()); recordsCopied++; }

            var saveResult = service.SavePlugin(targetMod, output ?? target);

            if (json)
                Console.WriteLine(new { success = saveResult.Success, result = new { recordsCopied, outputPath = output ?? target }, error = saveResult.Error }.ToJson());
            else if (saveResult.Success)
                Console.WriteLine($"Merged {recordsCopied} records from {source} into {output ?? target}");
            else
            { Console.Error.WriteLine($"Error: {saveResult.Error}"); Environment.ExitCode = 1; }
        }, sourceArg, targetArg, outputOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateDebugTypesCommand()
    {
        var patternArg = new Argument<string?>(
            "pattern",
            () => null,
            "Pattern to filter types (e.g., 'Quest*', 'QuestAlias')");
        var allOption = new Option<bool>("--all", "Show all Skyrim record types");

        var cmd = new Command("debug-types", "Show Mutagen type structures for debugging")
        {
            patternArg,
            allOption
        };

        cmd.SetHandler((pattern, all, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new TypeInspectionService(logger);

            var result = service.GetAllMutagenTypes(all ? "*" : pattern);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                foreach (var type in result.Value!)
                {
                    Console.WriteLine($"\n{type.Name} ({type.FullName})");
                    Console.WriteLine($"  Type: {(type.IsInterface ? "Interface" : "Class")}");

                    if (type.Properties.Count > 0)
                    {
                        Console.WriteLine("  Properties:");
                        foreach (var prop in type.Properties)
                        {
                            var nullable = prop.IsNullable ? "?" : "";
                            var collection = prop.IsCollection ? "[]" : "";
                            Console.WriteLine($"    {prop.Name}: {prop.Type}{nullable}{collection}");
                        }
                    }

                    if (type.Notes.Count > 0)
                    {
                        Console.WriteLine("  Notes:");
                        foreach (var note in type.Notes)
                            Console.WriteLine($"    - {note}");
                    }
                }

                Console.WriteLine($"\nFound {result.Value.Count} type(s)");
            }
            else
            {
                OutputError(result.Error!, json, result.Suggestions);
            }
        }, patternArg, allOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAutoFillCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var questOption = new Option<string>("--quest", "Quest EditorID") { IsRequired = true };
        var aliasOption = new Option<string?>("--alias", "Alias name (for alias scripts)");
        var scriptOption = new Option<string>("--script", "Script name") { IsRequired = true };
        var scriptDirOption = new Option<string>("--script-dir", "PSC source directory") { IsRequired = true };
        var dataFolderOption = new Option<string>("--data-folder", "Skyrim Data folder") { IsRequired = true };
        var noCacheOption = new Option<bool>("--no-cache", "Don't use cached link cache");

        var cmd = new Command("auto-fill", "Auto-fill script properties from PSC files")
        {
            pluginArg,
            questOption,
            aliasOption,
            scriptOption,
            scriptDirOption,
            dataFolderOption,
            noCacheOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var quest = context.ParseResult.GetValueForOption(questOption)!; // Required option
            var alias = context.ParseResult.GetValueForOption(aliasOption);
            var script = context.ParseResult.GetValueForOption(scriptOption)!; // Required option
            var scriptDir = context.ParseResult.GetValueForOption(scriptDirOption)!; // Required option
            var dataFolder = context.ParseResult.GetValueForOption(dataFolderOption)!; // Required option
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var pluginService = new PluginService(logger);
            var linkCacheManager = new LinkCacheManager(logger);
            var autoFillService = new AutoFillService(logger, linkCacheManager);

            // Clear cache if requested
            if (noCache)
            {
                linkCacheManager.ClearCache();
            }

            // Load plugin
            var loadResult = pluginService.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json, loadResult.Suggestions);
                return;
            }

            var mod = loadResult.Value!;

            // Get PSC file path
            var pscPath = Path.Combine(scriptDir, $"{script}.psc");

            // Auto-fill based on whether alias is specified
            Result<AutoFillResult> result;
            if (!string.IsNullOrEmpty(alias))
            {
                result = autoFillService.AutoFillAliasScript(mod, quest, alias, script, pscPath, dataFolder);
            }
            else
            {
                result = autoFillService.AutoFillQuestScript(mod, quest, script, pscPath, dataFolder);
            }

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                var data = result.Value!;
                Console.WriteLine($"Auto-fill complete for {data.ScriptName}:");
                Console.WriteLine($"  Filled: {data.FilledCount} properties");
                Console.WriteLine($"  Skipped: {data.SkippedCount} properties (primitives or already set)");
                Console.WriteLine($"  Not found: {data.NotFoundCount} properties (not in Skyrim.esm)");

                if (data.FilledProperties.Count > 0)
                {
                    Console.WriteLine("\nFilled properties:");
                    foreach (var prop in data.FilledProperties)
                        Console.WriteLine($"  - {prop}");
                }

                if (data.NotFoundProperties.Count > 0)
                {
                    Console.WriteLine("\nNot found in Skyrim.esm:");
                    foreach (var prop in data.NotFoundProperties)
                        Console.WriteLine($"  - {prop}");
                }

                // Save plugin
                var saveResult = pluginService.SavePlugin(mod, plugin);
                if (saveResult.Success)
                {
                    Console.WriteLine($"\nSaved: {plugin}");
                }
                else
                {
                    OutputError(saveResult.Error!, json);
                }
            }
            else
            {
                OutputError(result.Error!, json, result.Suggestions);
            }
        });

        return cmd;
    }

    private static Command CreateAutoFillAllCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var scriptDirOption = new Option<string>("--script-dir", "PSC source directory") { IsRequired = true };
        var dataFolderOption = new Option<string>("--data-folder", "Skyrim Data folder") { IsRequired = true };
        var noCacheOption = new Option<bool>("--no-cache", "Don't use cached link cache");

        var cmd = new Command("auto-fill-all", "Auto-fill all scripts in the mod")
        {
            pluginArg,
            scriptDirOption,
            dataFolderOption,
            noCacheOption
        };

        cmd.SetHandler((plugin, scriptDir, dataFolder, noCache, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var pluginService = new PluginService(logger);
            var linkCacheManager = new LinkCacheManager(logger);
            var autoFillService = new AutoFillService(logger, linkCacheManager);
            var bulkService = new BulkAutoFillService(logger, autoFillService, linkCacheManager);

            // Clear cache if requested
            if (noCache)
            {
                linkCacheManager.ClearCache();
            }

            // Load plugin
            var loadResult = pluginService.LoadPluginForEdit(plugin);
            if (!loadResult.Success)
            {
                OutputError(loadResult.Error!, json, loadResult.Suggestions);
                return;
            }

            var mod = loadResult.Value!;

            // Perform bulk auto-fill
            var result = bulkService.AutoFillAll(mod, scriptDir, dataFolder);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                var data = result.Value!;
                Console.WriteLine($"Bulk auto-fill complete:");
                Console.WriteLine($"  Scripts processed: {data.TotalScripts}");
                Console.WriteLine($"  Scripts with filled properties: {data.FilledScripts}");
                Console.WriteLine($"  Scripts skipped (no PSC): {data.SkippedScripts}");
                Console.WriteLine($"  Total properties filled: {data.TotalPropertiesFilled}");

                if (data.Errors.Count > 0)
                {
                    Console.WriteLine("\nErrors:");
                    foreach (var error in data.Errors.Take(10))
                        Console.WriteLine($"  - {error}");
                    if (data.Errors.Count > 10)
                        Console.WriteLine($"  ... and {data.Errors.Count - 10} more");
                }

                if (data.Details.Count > 0 && verbose)
                {
                    Console.WriteLine("\nDetails:");
                    foreach (var detail in data.Details.Take(20))
                        Console.WriteLine($"  {detail}");
                    if (data.Details.Count > 20)
                        Console.WriteLine($"  ... and {data.Details.Count - 20} more");
                }

                // Save plugin
                var saveResult = pluginService.SavePlugin(mod, plugin);
                if (saveResult.Success)
                {
                    Console.WriteLine($"\nSaved: {plugin}");
                }
                else
                {
                    OutputError(saveResult.Error!, json);
                }
            }
            else
            {
                OutputError(result.Error!, json, result.Suggestions);
            }
        }, pluginArg, scriptDirOption, dataFolderOption, noCacheOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddLeveledItemCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the leveled item");
        var nameOption = new Option<string?>("--name", "Display name for the leveled item");
        var chanceNoneOption = new Option<byte>("--chance-none", getDefaultValue: () => 0,
            description: "Chance (0-100) that the list returns nothing");
        var addEntryOption = new Option<string[]?>("--add-entry",
            description: "Add entry in format: editorId,level,count (e.g., GoldBase,1,50). Can be used multiple times.");
        var presetOption = new Option<string?>("--preset",
            description: "Use preset: low-treasure, medium-treasure, high-treasure, guaranteed-loot");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-leveled-item", "Add a leveled item list record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, chanceNoneOption, addEntryOption, presetOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var chanceNone = context.ParseResult.GetValueForOption(chanceNoneOption);
            var entries = context.ParseResult.GetValueForOption(addEntryOption);
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new LeveledItemBuilder(mod, editorId);

            builder.WithChanceNone(chanceNone);

            // Apply preset
            if (!string.IsNullOrEmpty(preset))
            {
                switch (preset.ToLowerInvariant())
                {
                    case "low-treasure": builder.AsLowTreasure(); break;
                    case "medium-treasure": builder.AsMediumTreasure(); break;
                    case "high-treasure": builder.AsHighTreasure(); break;
                    case "guaranteed-loot": builder.AsGuaranteedLoot(); break;
                    default:
                        OutputError($"Unknown preset: {preset}. Use: low-treasure, medium-treasure, high-treasure, guaranteed-loot", json);
                        return;
                }
            }

            // Add entries
            if (entries != null && entries.Length > 0)
            {
                foreach (var entry in entries)
                {
                    var parts = entry.Split(',');
                    if (parts.Length < 2 || parts.Length > 3)
                    {
                        OutputError($"Invalid entry format: {entry}. Use: editorId,level[,count]", json);
                        return;
                    }

                    var itemEditorId = parts[0];
                    if (!short.TryParse(parts[1], out var level))
                    {
                        OutputError($"Invalid level in entry: {entry}", json);
                        return;
                    }

                    short count = 1;
                    if (parts.Length == 3 && !short.TryParse(parts[2], out count))
                    {
                        OutputError($"Invalid count in entry: {entry}", json);
                        return;
                    }

                    // Try to find the item in the mod (search weapons, armors, misc items, etc.)
                    FormKey? itemFormKey = null;
                    var weapon = mod.Weapons.FirstOrDefault(w => w.EditorID == itemEditorId);
                    if (weapon != null)
                    {
                        itemFormKey = weapon.FormKey;
                    }
                    else
                    {
                        var armor = mod.Armors.FirstOrDefault(a => a.EditorID == itemEditorId);
                        if (armor != null)
                        {
                            itemFormKey = armor.FormKey;
                        }
                        else
                        {
                            var miscItem = mod.MiscItems.FirstOrDefault(m => m.EditorID == itemEditorId);
                            if (miscItem != null)
                            {
                                itemFormKey = miscItem.FormKey;
                            }
                        }
                    }

                    if (!itemFormKey.HasValue)
                    {
                        OutputError($"Item not found: {itemEditorId}", json);
                        return;
                    }

                    builder.AddEntry(itemFormKey.Value, level, count);
                }
            }

            var leveledItem = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = leveledItem.EditorID,
                            formId = leveledItem.FormKey.ToString(),
                            chanceNone = leveledItem.ChanceNone,
                            entryCount = leveledItem.Entries?.Count ?? 0,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added leveled item: {leveledItem.EditorID} ({leveledItem.FormKey}) - {leveledItem.Entries?.Count ?? 0} entries";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateSetArmorAddonModelCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID of the ArmorAddon to modify");
        var maleOption = new Option<string?>("--male", "Male world model path, relative to Data (e.g. armor\\foo\\bar.nif)");
        var femaleOption = new Option<string?>("--female", "Female world model path, relative to Data");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("set-armor-addon-model",
            "Set an ArmorAddon's male/female world model paths. Note Skyrim appends the _0/_1 body-weight " +
            "suffix itself - the path stored here must NOT include it, or body-weight fitting breaks.")
        {
            pluginArg, editorIdArg, maleOption, femaleOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var male = context.ParseResult.GetValueForOption(maleOption);
            var female = context.ParseResult.GetValueForOption(femaleOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var addon = mod.ArmorAddons.FirstOrDefault(a =>
                a.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
            if (addon == null)
            {
                OutputError($"ArmorAddon '{editorId}' not found in {Path.GetFileName(plugin)}.", json);
                return;
            }

            addon.WorldModel ??= new GenderedItem<Model?>(null, null);

            if (!string.IsNullOrEmpty(male))
            {
                addon.WorldModel.Male ??= new Model();
                addon.WorldModel.Male.File.GivenPath = male;
            }
            if (!string.IsNullOrEmpty(female))
            {
                addon.WorldModel.Female ??= new Model();
                addon.WorldModel.Female.File.GivenPath = female;
            }

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                Console.WriteLine(saveResult.Success
                    ? (object)new { success = true, result = new { editorId = addon.EditorID, male, female, dryRun } }.ToJson()
                    : new { success = false, error = saveResult.Error }.ToJson());
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Updated '{addon.EditorID}' world model(s):");
                if (!string.IsNullOrEmpty(male)) { Console.WriteLine($"  male:   {male}"); }
                if (!string.IsNullOrEmpty(female)) { Console.WriteLine($"  female: {female}"); }
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddKeywordCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the keyword");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-keyword",
            "Add a Keyword (KYWD) record to a plugin. Useful as a stable tag for runtime patchers " +
            "such as SkyPatcher to attach onto records the plugin does not own.")
        {
            pluginArg, editorIdArg, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;

            // Refuse duplicates rather than silently creating a second keyword with the same
            // EditorID - a patcher matching by EditorID would then bind to an arbitrary one.
            var existing = mod.Keywords.FirstOrDefault(k =>
                k.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
            if (existing != null)
            {
                OutputError($"Keyword '{editorId}' already exists in this plugin ({existing.FormKey}).", json);
                return;
            }

            var keyword = new KeywordBuilder(mod, editorId).Build();

            Result saveResult = dryRun
                ? Result.Ok($"{plugin} (DRY RUN - not saved)")
                : service.SavePlugin(mod, plugin);

            if (json)
            {
                Console.WriteLine(saveResult.Success
                    ? (object)new { success = true, result = new { editorId = keyword.EditorID, formId = keyword.FormKey.ToString(), dryRun } }.ToJson()
                    : new { success = false, error = saveResult.Error }.ToJson());
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Added keyword '{keyword.EditorID}' ({keyword.FormKey})");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }

            if (!saveResult.Success) { Environment.ExitCode = 1; }
        });

        return cmd;
    }

    private static Command CreateAddFormListCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the form list");
        var addFormOption = new Option<string[]?>("--add-form",
            description: "Add form in format: FormId:ModName.esp (no 0x prefix) or EditorId. Can be used multiple times.");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-form-list", "Add a form list record to a plugin")
        {
            pluginArg, editorIdArg, addFormOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var forms = context.ParseResult.GetValueForOption(addFormOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new FormListBuilder(mod, editorId);

            // Add forms
            if (forms != null && forms.Length > 0)
            {
                foreach (var formStr in forms)
                {
                    // Try to parse as FormKey first
                    if (Mutagen.Bethesda.Plugins.FormKey.TryFactory(formStr, out var formKey))
                    {
                        builder.AddForm(formKey);
                    }
                    else
                    {
                        // Try to find by EditorID in the mod
                        var record = mod.EnumerateMajorRecords().FirstOrDefault(r => r.EditorID == formStr);
                        if (record == null)
                        {
                            OutputError($"Form not found: {formStr}", json);
                            return;
                        }
                        builder.AddForm(record.FormKey);
                    }
                }
            }

            var formList = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = formList.EditorID,
                            formId = formList.FormKey.ToString(),
                            formCount = formList.Items?.Count ?? 0,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added form list: {formList.EditorID} ({formList.FormKey}) - {formList.Items?.Count ?? 0} forms";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddEncounterZoneCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the encounter zone");
        var minLevelOption = new Option<byte>("--min-level", getDefaultValue: () => 1,
            description: "Minimum level");
        var maxLevelOption = new Option<byte>("--max-level", getDefaultValue: () => 0,
            description: "Maximum level (0 = unlimited)");
        var neverResetsOption = new Option<bool>("--never-resets", "Zone never resets");
        var presetOption = new Option<string?>("--preset",
            description: "Use preset: low-level, mid-level, high-level, scaling");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-encounter-zone", "Add an encounter zone record to a plugin")
        {
            pluginArg, editorIdArg, minLevelOption, maxLevelOption, neverResetsOption, presetOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var minLevel = context.ParseResult.GetValueForOption(minLevelOption);
            var maxLevel = context.ParseResult.GetValueForOption(maxLevelOption);
            var neverResets = context.ParseResult.GetValueForOption(neverResetsOption);
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new EncounterZoneBuilder(mod, editorId);

            // Apply preset
            if (!string.IsNullOrEmpty(preset))
            {
                switch (preset.ToLowerInvariant())
                {
                    case "low-level": builder.AsLowLevel(); break;
                    case "mid-level": builder.AsMidLevel(); break;
                    case "high-level": builder.AsHighLevel(); break;
                    case "scaling": builder.AsScaling(); break;
                    default:
                        OutputError($"Unknown preset: {preset}. Use: low-level, mid-level, high-level, scaling", json);
                        return;
                }
            }
            else
            {
                builder.WithMinLevel(minLevel);
                builder.WithMaxLevel(maxLevel);
            }

            if (neverResets) builder.NeverResets();

            var encounterZone = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = encounterZone.EditorID,
                            formId = encounterZone.FormKey.ToString(),
                            minLevel = encounterZone.MinLevel,
                            maxLevel = encounterZone.MaxLevel,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added encounter zone: {encounterZone.EditorID} ({encounterZone.FormKey}) - Level {encounterZone.MinLevel}-{(encounterZone.MaxLevel == 0 ? "unlimited" : encounterZone.MaxLevel.ToString())}";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddLocationCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the location");
        var nameOption = new Option<string?>("--name", "Display name for the location");
        var parentOption = new Option<string?>("--parent-location",
            description: "Parent location EditorID or FormKey");
        var keywordOption = new Option<string[]?>("--add-keyword",
            description: "Add keyword by EditorID or FormKey. Can be used multiple times.");
        var presetOption = new Option<string?>("--preset",
            description: "Use preset: inn, city, dungeon, dwelling");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-location", "Add a location record to a plugin")
        {
            pluginArg, editorIdArg, nameOption, parentOption, keywordOption, presetOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var parent = context.ParseResult.GetValueForOption(parentOption);
            var keywords = context.ParseResult.GetValueForOption(keywordOption);
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new LocationBuilder(mod, editorId);

            if (!string.IsNullOrEmpty(name)) builder.WithName(name);

            // Apply preset
            if (!string.IsNullOrEmpty(preset))
            {
                switch (preset.ToLowerInvariant())
                {
                    case "inn": builder.AsInn(); break;
                    case "city": builder.AsCity(); break;
                    case "dungeon": builder.AsDungeon(); break;
                    case "dwelling": builder.AsDwelling(); break;
                    default:
                        OutputError($"Unknown preset: {preset}. Use: inn, city, dungeon, dwelling", json);
                        return;
                }
            }

            // Add parent location
            if (!string.IsNullOrEmpty(parent))
            {
                if (Mutagen.Bethesda.Plugins.FormKey.TryFactory(parent, out var parentFormKey))
                {
                    builder.WithParentLocation(parentFormKey);
                }
                else
                {
                    var parentLoc = mod.Locations.FirstOrDefault(l => l.EditorID == parent);
                    if (parentLoc == null)
                    {
                        OutputError($"Parent location not found: {parent}", json);
                        return;
                    }
                    builder.WithParentLocation(parentLoc.FormKey);
                }
            }

            // Add keywords
            if (keywords != null && keywords.Length > 0)
            {
                foreach (var keywordStr in keywords)
                {
                    if (Mutagen.Bethesda.Plugins.FormKey.TryFactory(keywordStr, out var keywordFormKey))
                    {
                        builder.AddKeyword(keywordFormKey);
                    }
                    else
                    {
                        var keyword = mod.Keywords.FirstOrDefault(k => k.EditorID == keywordStr);
                        if (keyword == null)
                        {
                            OutputError($"Keyword not found: {keywordStr}", json);
                            return;
                        }
                        builder.AddKeyword(keyword.FormKey);
                    }
                }
            }

            var location = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = location.EditorID,
                            formId = location.FormKey.ToString(),
                            name = location.Name?.String,
                            keywordCount = location.Keywords?.Count ?? 0,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added location: {location.EditorID} ({location.FormKey})";
                if (!string.IsNullOrEmpty(location.Name?.String)) msg += $" - {location.Name}";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAddOutfitCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID for the outfit");
        var addItemOption = new Option<string[]?>("--add-item",
            description: "Add item (armor/weapon) by EditorID or FormKey. Can be used multiple times.");
        var presetOption = new Option<string?>("--preset",
            description: "Use preset: guard, farmer, mage, thief");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("add-outfit", "Add an outfit record to a plugin")
        {
            pluginArg, editorIdArg, addItemOption, presetOption, dryRunOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var items = context.ParseResult.GetValueForOption(addItemOption);
            var preset = context.ParseResult.GetValueForOption(presetOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;
            var builder = new OutfitBuilder(mod, editorId);

            // Apply preset
            if (!string.IsNullOrEmpty(preset))
            {
                switch (preset.ToLowerInvariant())
                {
                    case "guard": builder.AsGuard(); break;
                    case "farmer": builder.AsFarmer(); break;
                    case "mage": builder.AsMage(); break;
                    case "thief": builder.AsThief(); break;
                    default:
                        OutputError($"Unknown preset: {preset}. Use: guard, farmer, mage, thief", json);
                        return;
                }
            }

            // Add items
            if (items != null && items.Length > 0)
            {
                foreach (var itemStr in items)
                {
                    if (Mutagen.Bethesda.Plugins.FormKey.TryFactory(itemStr, out var itemFormKey))
                    {
                        builder.AddItem(itemFormKey);
                    }
                    else
                    {
                        // Search in armor and weapon collections
                        var armor = mod.Armors.FirstOrDefault(a => a.EditorID == itemStr);
                        if (armor != null)
                        {
                            builder.AddItem(armor.FormKey);
                        }
                        else
                        {
                            var weapon = mod.Weapons.FirstOrDefault(w => w.EditorID == itemStr);
                            if (weapon == null)
                            {
                                OutputError($"Item not found: {itemStr}", json);
                                return;
                            }
                            builder.AddItem(weapon.FormKey);
                        }
                    }
                }
            }

            var outfit = builder.Build();

            // Conditional save based on dry-run
            Result saveResult;
            if (dryRun)
            {
                saveResult = Result.Ok($"{plugin} (DRY RUN - not saved)");
            }
            else
            {
                saveResult = service.SavePlugin(mod, plugin);
            }

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            editorId = outfit.EditorID,
                            formId = outfit.FormKey.ToString(),
                            itemCount = outfit.Items?.Count ?? 0,
                            dryRun
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                var msg = $"Added outfit: {outfit.EditorID} ({outfit.FormKey}) - {outfit.Items?.Count ?? 0} items";
                if (dryRun) msg += " [DRY RUN - not saved]";
                Console.WriteLine(msg);
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static void OutputError(string error, bool json, IEnumerable<string>? suggestions = null)
    {
        if (json)
        {
            Console.WriteLine(Result.Fail(error, suggestions: suggestions?.ToList()).ToJson(true));
        }
        else
        {
            Console.Error.WriteLine($"Error: {error}");
            if (suggestions != null)
            {
                Console.Error.WriteLine("Suggestions:");
                foreach (var s in suggestions)
                    Console.Error.WriteLine($"  - {s}");
            }
        }
        Environment.ExitCode = 1;
    }

    private static Command CreateViewRecordCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdOption = new Option<string?>("--editor-id", "EditorID of the record to view");
        var formIdOption = new Option<string?>("--form-id", "FormID of the record to view (e.g., 0x000800)");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");
        var includeRawOption = new Option<bool>("--include-raw", "Include raw properties via reflection");

        var cmd = new Command("view-record", "View detailed information about a record")
        {
            pluginArg,
            editorIdOption,
            formIdOption,
            typeOption,
            includeRawOption
        };

        cmd.SetHandler((plugin, editorId, formId, type, includeRaw, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.ViewRecord(plugin, editorId, formId, type, includeRaw);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var record = result.Value;
                Console.WriteLine($"Record: {record.EditorId}");
                Console.WriteLine($"FormKey: {record.FormKey}");
                Console.WriteLine($"Type: {record.RecordType}");
                Console.WriteLine();
                Console.WriteLine("Properties:");
                foreach (var (key, value) in record.Properties)
                {
                    if (value is List<Dictionary<string, object?>> list)
                    {
                        Console.WriteLine($"  {key}:");
                        foreach (var item in list)
                        {
                            Console.WriteLine($"    - {string.Join(", ", item.Select(kv => $"{kv.Key}={kv.Value}"))}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  {key}: {value}");
                    }
                }

                if (record.Conditions != null && record.Conditions.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Conditions:");
                    for (int i = 0; i < record.Conditions.Count; i++)
                    {
                        var cond = record.Conditions[i];
                        Console.WriteLine($"  [{i}] {cond.FunctionName} {cond.Operator} {cond.ComparisonValue}");
                        if (!string.IsNullOrEmpty(cond.ParameterA))
                            Console.WriteLine($"      ParamA: {cond.ParameterA}");
                        if (!string.IsNullOrEmpty(cond.ParameterB))
                            Console.WriteLine($"      ParamB: {cond.ParameterB}");
                        Console.WriteLine($"      Flags: {cond.Flags}, RunOn: {cond.RunOn}");
                    }
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, pluginArg, editorIdOption, formIdOption, typeOption, includeRawOption,
           _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateOverrideCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Name of the output patch plugin (e.g., Patch.esp)");
        var editorIdOption = new Option<string?>("--editor-id", "EditorID of the record to override");
        var formIdOption = new Option<string?>("--form-id", "FormID of the record to override");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");
        var dataFolderOption = new Option<string?>("--data-folder", "Data folder path (defaults to source plugin directory)");

        outputOption.IsRequired = true;

        var cmd = new Command("create-override", "Create an override patch for a record")
        {
            sourceArg,
            outputOption,
            editorIdOption,
            formIdOption,
            typeOption,
            dataFolderOption
        };

        cmd.SetHandler((source, output, editorId, formId, type, dataFolder, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.CreateOverride(
                source,
                output,
                editorId,
                formId,
                type,
                false, // removeConditions - not yet implemented
                dataFolder);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Created override patch: {result.Value}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, sourceArg, outputOption, editorIdOption, formIdOption, typeOption,
           dataFolderOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateFindRecordCommand()
    {
        var searchOption = new Option<string?>("--search", "Search pattern for EditorID or Name");
        var editorIdOption = new Option<string?>("--editor-id", "Exact EditorID to find");
        var typeOption = new Option<string?>("--type", "Record type to filter by");
        var pluginOption = new Option<string?>("--plugin", "Path to specific plugin to search");
        var dataFolderOption = new Option<string?>("--data-folder", "Data folder to search all plugins");
        var allPluginsOption = new Option<bool>("--all-plugins", "Search all plugins in data folder");

        var cmd = new Command("find-record", "Find records across plugins")
        {
            searchOption,
            editorIdOption,
            typeOption,
            pluginOption,
            dataFolderOption,
            allPluginsOption
        };

        cmd.SetHandler((search, editorId, type, plugin, dataFolder, allPlugins, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.FindRecords(search, editorId, type, plugin, dataFolder, allPlugins);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var records = result.Value;
                Console.WriteLine($"Found {records.Count} record(s):");
                Console.WriteLine();

                foreach (var record in records)
                {
                    Console.WriteLine($"Plugin: {record.PluginName}");
                    Console.WriteLine($"  EditorID: {record.EditorId}");
                    Console.WriteLine($"  FormKey: {record.FormKey}");
                    Console.WriteLine($"  Type: {record.RecordType}");
                    if (!string.IsNullOrEmpty(record.Name))
                        Console.WriteLine($"  Name: {record.Name}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, searchOption, editorIdOption, typeOption, pluginOption, dataFolderOption,
           allPluginsOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateListRecordsCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var typeOption = new Option<string?>("--type", "Record type to filter (e.g., weapon, armor, npc, quest, spell)");
        var limitOption = new Option<int>("--limit", () => 100, "Maximum number of records to return");

        var cmd = new Command("list-records", "List all records in a plugin, optionally filtered by type")
        {
            pluginArg, typeOption, limitOption
        };

        cmd.SetHandler((plugin, type, limit, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            // Use FindRecords with wildcard pattern
            var result = service.FindRecords("*", null, type, plugin, null, false);

            if (json)
            {
                if (result.Success && result.Value != null)
                {
                    var records = result.Value.Take(limit).ToList();
                    Console.WriteLine(Result<List<RecordSearchResult>>.Ok(records).ToJson(true));
                }
                else
                {
                    Console.WriteLine(result.ToJson(true));
                }
            }
            else if (result.Success && result.Value != null)
            {
                var records = result.Value;
                var total = records.Count;
                var displayed = records.Take(limit).ToList();

                Console.WriteLine($"Records in {Path.GetFileName(plugin)}:");
                if (!string.IsNullOrEmpty(type))
                    Console.WriteLine($"Filter: {type}");
                Console.WriteLine($"Total: {total}{(total > limit ? $" (showing first {limit})" : "")}");
                Console.WriteLine();

                foreach (var record in displayed)
                {
                    var nameStr = !string.IsNullOrEmpty(record.Name) ? $" - {record.Name}" : "";
                    Console.WriteLine($"  [{record.RecordType}] {record.EditorId ?? "(no EditorID)"}{nameStr} ({record.FormKey})");
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.Suggestions != null)
                {
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, pluginArg, typeOption, limitOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateBatchOverrideCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Name of the output patch plugin");
        var typeOption = new Option<string?>("--type", "Record type to filter");
        var searchOption = new Option<string?>("--search", "Search pattern for EditorIDs");
        var editorIdsOption = new Option<string?>("--editor-ids", "Comma-separated list of EditorIDs");
        var dataFolderOption = new Option<string?>("--data-folder", "Data folder path");

        outputOption.IsRequired = true;

        var cmd = new Command("batch-override", "Create override patches for multiple records")
        {
            sourceArg,
            outputOption,
            typeOption,
            searchOption,
            editorIdsOption,
            dataFolderOption
        };

        cmd.SetHandler((source, output, type, search, editorIdsStr, dataFolder, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            string[]? editorIds = null;
            if (!string.IsNullOrEmpty(editorIdsStr))
            {
                editorIds = editorIdsStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }

            var result = service.BatchOverride(source, type, search, editorIds, output, dataFolder);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var batchResult = result.Value;
                Console.WriteLine($"Created batch override patch: {batchResult.PatchPath}");
                Console.WriteLine($"Modified {batchResult.RecordsModified} record(s):");
                foreach (var record in batchResult.ModifiedRecords)
                {
                    Console.WriteLine($"  - {record}");
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, sourceArg, outputOption, typeOption, searchOption, editorIdsOption,
           dataFolderOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateRemoveScriptCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Name of the output patch plugin");
        var typeOption = new Option<string?>("--type", "Record type to filter");
        var searchOption = new Option<string?>("--search", "Search pattern for EditorIDs");
        var editorIdsOption = new Option<string?>("--editor-ids", "Comma-separated list of EditorIDs");
        var scriptNameOption = new Option<string?>(
            "--script",
            "Name of the script to remove (without .pex). Omit to strip ALL scripts (the entire VirtualMachineAdapter) from each matched record");
        var dataFolderOption = new Option<string?>("--data-folder", "Data folder path");
        var lightOption = new Option<bool>("--light", "Flag the output patch as ESL (light master)");

        outputOption.IsRequired = true;

        var cmd = new Command(
            "remove-script",
            "Remove Papyrus script data (VMAD) from one or more records, creating an override patch. Works on any record type that carries scripts (Book, Weapon, Armor, NPC, Container, etc.)")
        {
            sourceArg,
            outputOption,
            typeOption,
            searchOption,
            editorIdsOption,
            scriptNameOption,
            dataFolderOption,
            lightOption
        };

        cmd.SetHandler((context) =>
        {
            var source = context.ParseResult.GetValueForArgument(sourceArg);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var type = context.ParseResult.GetValueForOption(typeOption);
            var search = context.ParseResult.GetValueForOption(searchOption);
            var editorIdsStr = context.ParseResult.GetValueForOption(editorIdsOption);
            var scriptName = context.ParseResult.GetValueForOption(scriptNameOption);
            var dataFolder = context.ParseResult.GetValueForOption(dataFolderOption);
            var light = context.ParseResult.GetValueForOption(lightOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            string[]? editorIds = null;
            if (!string.IsNullOrEmpty(editorIdsStr))
            {
                editorIds = editorIdsStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }

            var result = service.RemoveScript(source!, type, search, editorIds, scriptName, output!, dataFolder, light);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var batchResult = result.Value;
                Console.WriteLine($"Created script-removal patch: {batchResult.PatchPath}");
                Console.WriteLine($"Removed {batchResult.ScriptsRemoved} script(s) across {batchResult.RecordsModified} record(s):");
                foreach (var record in batchResult.ModifiedRecords)
                {
                    Console.WriteLine($"  - {record}");
                }
                if (batchResult.SkippedRecords.Count > 0)
                {
                    Console.WriteLine($"Skipped {batchResult.SkippedRecords.Count} record(s):");
                    foreach (var skipped in batchResult.SkippedRecords)
                    {
                        Console.WriteLine($"  - {skipped}");
                    }
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateBatchRenameCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Name of the output patch plugin");
        var typeOption = new Option<string>("--type", "Record type to filter (currently only 'weapon' is supported)");
        var editorIdsOption = new Option<string?>("--editor-ids", "Comma-separated list of EditorIDs to rename");
        var suffixOption = new Option<string>("--append-suffix", "Text appended to the end of each record's existing Name");
        var dataFolderOption = new Option<string?>("--data-folder", "Data folder path");

        outputOption.IsRequired = true;
        typeOption.IsRequired = true;
        suffixOption.IsRequired = true;

        var cmd = new Command("batch-rename", "Create override patches for multiple records, appending a suffix to their display Name")
        {
            sourceArg,
            outputOption,
            typeOption,
            editorIdsOption,
            suffixOption,
            dataFolderOption
        };

        cmd.SetHandler((source, output, type, editorIdsStr, suffix, dataFolder, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var editorIds = (editorIdsStr ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var result = service.BatchAppendNameSuffix(source, editorIds, type, suffix, output, dataFolder);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var batchResult = result.Value;
                Console.WriteLine($"Created batch rename patch: {batchResult.PatchPath}");
                Console.WriteLine($"Renamed {batchResult.RecordsModified} record(s):");
                foreach (var record in batchResult.ModifiedRecords)
                {
                    Console.WriteLine($"  - {record}");
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, sourceArg, outputOption, typeOption, editorIdsOption, suffixOption,
           dataFolderOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateCompareRecordCommand()
    {
        var plugin1Arg = new Argument<string>("plugin1", "Path to first plugin");
        var plugin2Arg = new Argument<string>("plugin2", "Path to second plugin");
        var editorIdOption = new Option<string?>("--editor-id", "EditorID of the record to compare");
        var formIdOption = new Option<string?>("--form-id", "FormID of the record to compare");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");

        var cmd = new Command("compare-record", "Compare two versions of the same record")
        {
            plugin1Arg,
            plugin2Arg,
            editorIdOption,
            formIdOption,
            typeOption
        };

        cmd.SetHandler((plugin1, plugin2, editorId, formId, type, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.CompareRecords(plugin1, plugin2, editorId, formId, type);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var comparison = result.Value;

                Console.WriteLine($"Comparing: {comparison.Original.EditorId} ({comparison.Original.FormKey})");
                Console.WriteLine($"Plugin 1: {plugin1}");
                Console.WriteLine($"Plugin 2: {plugin2}");
                Console.WriteLine();

                if (comparison.Differences.Count == 0)
                {
                    Console.WriteLine("No differences found - records are identical");
                }
                else
                {
                    Console.WriteLine($"Found {comparison.Differences.Count} difference(s):");
                    Console.WriteLine();

                    foreach (var (field, diff) in comparison.Differences)
                    {
                        Console.WriteLine($"Field: {field}");
                        Console.WriteLine($"  Original:  {diff.OriginalValue ?? "(null)"}");
                        Console.WriteLine($"  Modified:  {diff.ModifiedValue ?? "(null)"}");
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, plugin1Arg, plugin2Arg, editorIdOption, formIdOption, typeOption,
           _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateConflictsCommand()
    {
        var dataFolderArg = new Argument<string>("data-folder", "Path to Skyrim Data folder");
        var pluginOption = new Option<string?>("--plugin", "Check conflicts for this plugin");
        var editorIdOption = new Option<string?>("--editor-id", "Check conflicts for this EditorID");
        var formIdOption = new Option<string?>("--form-id", "Check conflicts for this FormID");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");

        var cmd = new Command("conflicts", "Detect load order conflicts")
        {
            dataFolderArg,
            pluginOption,
            editorIdOption,
            formIdOption,
            typeOption
        };

        cmd.SetHandler((dataFolder, plugin, editorId, formId, type, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.DetectConflicts(plugin, editorId, formId, type, dataFolder);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var report = result.Value;

                Console.WriteLine("Conflict Report:");
                if (!string.IsNullOrEmpty(report.EditorId))
                    Console.WriteLine($"EditorID: {report.EditorId}");
                if (!string.IsNullOrEmpty(report.FormKey))
                    Console.WriteLine($"FormKey: {report.FormKey}");
                Console.WriteLine();

                Console.WriteLine($"Found {report.Conflicts.Count} plugin(s) modifying this record:");
                Console.WriteLine();

                foreach (var conflict in report.Conflicts)
                {
                    var winner = conflict.IsWinner ? " [WINNER]" : "";
                    Console.WriteLine($"[{conflict.LoadOrder:D3}] {conflict.PluginName}{winner}");
                }

                Console.WriteLine();
                Console.WriteLine($"Winning override: {report.WinningPlugin}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, dataFolderArg, pluginOption, editorIdOption, formIdOption, typeOption,
           _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateListConditionsCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdOption = new Option<string?>("--editor-id", "EditorID of the record");
        var formIdOption = new Option<string?>("--form-id", "FormID of the record");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");

        var cmd = new Command("list-conditions", "List all conditions on a record")
        {
            pluginArg,
            editorIdOption,
            formIdOption,
            typeOption
        };

        cmd.SetHandler((plugin, editorId, formId, type, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.ListConditions(plugin, editorId, formId, type);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success && result.Value != null)
            {
                var conditions = result.Value;

                if (conditions.Count == 0)
                {
                    Console.WriteLine("No conditions found on this record");
                }
                else
                {
                    Console.WriteLine($"Found {conditions.Count} condition(s):");
                    Console.WriteLine();

                    for (int i = 0; i < conditions.Count; i++)
                    {
                        var cond = conditions[i];
                        Console.WriteLine($"[{i}] {cond.FunctionName}");
                        Console.WriteLine($"    Operator: {cond.Operator}");
                        Console.WriteLine($"    Comparison: {cond.ComparisonValue}");
                        if (!string.IsNullOrEmpty(cond.Flags))
                            Console.WriteLine($"    Flags: {cond.Flags}");
                        if (!string.IsNullOrEmpty(cond.RunOn))
                            Console.WriteLine($"    RunOn: {cond.RunOn}");
                        if (!string.IsNullOrEmpty(cond.ParameterA))
                            Console.WriteLine($"    ParamA: {cond.ParameterA}");
                        if (!string.IsNullOrEmpty(cond.ParameterB))
                            Console.WriteLine($"    ParamB: {cond.ParameterB}");
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, pluginArg, editorIdOption, formIdOption, typeOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateRemoveConditionCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Name of the output patch plugin");
        var editorIdOption = new Option<string?>("--editor-id", "EditorID of the record");
        var formIdOption = new Option<string?>("--form-id", "FormID of the record");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");
        var indicesOption = new Option<string>(
            "--indices",
            description: "Comma-separated indices of conditions to remove (e.g., \"0,2,5\")");

        outputOption.IsRequired = true;
        indicesOption.IsRequired = true;

        var cmd = new Command("remove-condition", "Remove specific conditions from a record")
        {
            sourceArg,
            outputOption,
            editorIdOption,
            formIdOption,
            typeOption,
            indicesOption
        };

        cmd.SetHandler((source, output, editorId, formId, type, indicesStr, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            // Parse indices
            int[] indices;
            try
            {
                indices = indicesStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse)
                    .ToArray();
            }
            catch
            {
                Console.Error.WriteLine("Error: Invalid indices format. Use comma-separated numbers (e.g., \"0,2,5\")");
                Environment.ExitCode = 1;
                return;
            }

            var result = service.RemoveConditions(source, editorId, formId, type, indices, output, null);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Created patch with {indices.Length} condition(s) removed:");
                Console.WriteLine(result.Value);
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, sourceArg, outputOption, editorIdOption, formIdOption, typeOption,
           indicesOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddConditionCommand()
    {
        var sourceArg = new Argument<string>("source", "Path to the source plugin");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Name of the output patch plugin");
        var editorIdOption = new Option<string?>("--editor-id", "EditorID of the record");
        var formIdOption = new Option<string?>("--form-id", "FormID of the record");
        var typeOption = new Option<string?>("--type", "Record type (required with --editor-id)");
        var functionOption = new Option<string>(
            "--function",
            description: "Condition function (e.g., GetLevel, IsSneaking)");
        var valueOption = new Option<float>(
            "--value",
            getDefaultValue: () => 1.0f,
            description: "Comparison value (default: 1.0, uses >= operator)");

        outputOption.IsRequired = true;
        functionOption.IsRequired = true;

        var cmd = new Command("add-condition", "Add a condition to a record (uses >= 1.0 comparison)")
        {
            sourceArg,
            outputOption,
            editorIdOption,
            formIdOption,
            typeOption,
            functionOption
        };

        cmd.SetHandler((source, output, editorId, formId, type, function, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            // Note: Value hardcoded to 1.0f to stay within 8-parameter SetHandler limit
            // Most conditions use 1.0 for true/false checks
            var result = service.AddCondition(source, editorId, formId, type, function, 1.0f, "GreaterThanOrEqualTo", output, null);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Created patch with new {function} condition:");
                Console.WriteLine(result.Value);
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.ErrorContext != null)
                    Console.Error.WriteLine($"Context: {result.ErrorContext}");
                if (result.Suggestions != null)
                {
                    Console.Error.WriteLine("Suggestions:");
                    foreach (var s in result.Suggestions)
                        Console.Error.WriteLine($"  - {s}");
                }
                Environment.ExitCode = 1;
            }
        }, sourceArg, outputOption, editorIdOption, formIdOption, typeOption,
           functionOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateAddPackageCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editor-id", "Editor ID for the package");
        var typeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            description: "Package type (sandbox, travel, sleep, eat, follow, guard, patrol, useitemat, sit, useidlemarker, flee, accompany, castmagic, dialogue, find, ambush, wander, wait, activate, relax, forcegreet, greet, useweapon, usemagic, lockdoors, unlockdoors, dismount, acquire, escortto, say, shout, followto, holdposition, keepaneyeon, hover, orbit)"
        ) { IsRequired = true };
        var radiusOption = new Option<ushort>(
            "--radius",
            getDefaultValue: () => 500,
            description: "Radius for sandbox packages (in units)");
        var locationOption = new Option<string?>(
            "--location",
            description: "FormKey of location reference");
        var bedRefOption = new Option<string?>(
            "--bed",
            description: "FormKey of bed reference (for sleep packages)");
        var furnitureRefOption = new Option<string?>(
            "--furniture",
            description: "FormKey of furniture reference (for eat packages)");
        var targetRefOption = new Option<string?>(
            "--target",
            description: "FormKey of target reference (for follow packages)");
        var markerRefOption = new Option<string?>(
            "--marker",
            description: "FormKey of marker reference (for guard packages)");
        var startHourOption = new Option<byte>(
            "--start-hour",
            getDefaultValue: () => 22,
            description: "Start hour for schedule (0-23)");
        var durationOption = new Option<byte>(
            "--duration",
            getDefaultValue: () => 8,
            description: "Duration in hours");
        var itemRefOption = new Option<string?>(
            "--item-ref",
            description: "FormKey of item/object reference (for useitemat packages)");
        var fleeFromOption = new Option<string?>(
            "--flee-from",
            description: "FormKey of reference to flee from (optional for flee packages)");
        var distanceOption = new Option<ushort>(
            "--distance",
            getDefaultValue: () => 1000,
            description: "Distance for flee packages (in units)");
        var destinationRefOption = new Option<string?>(
            "--destination",
            description: "FormKey of destination reference (for accompany packages)");
        var weaponRefOption = new Option<string?>(
            "--weapon-ref",
            description: "FormKey of weapon reference (for useweapon packages)");
        var spellRefOption = new Option<string?>(
            "--spell-ref",
            description: "FormKey of spell reference (for usemagic packages)");
        var doorRefOption = new Option<string?>(
            "--door-ref",
            description: "FormKey of door reference (for lockdoors/unlockdoors packages)");
        var objectRefOption = new Option<string?>(
            "--object-ref",
            description: "FormKey of object reference (for acquire packages)");
        var escortRefOption = new Option<string?>(
            "--escort-ref",
            description: "FormKey of actor to escort (for escortto packages)");
        var topicRefOption = new Option<string?>(
            "--topic-ref",
            description: "FormKey of dialogue topic (for say packages)");
        var shoutRefOption = new Option<string?>(
            "--shout-ref",
            description: "FormKey of shout (for shout packages)");
        var followRefOption = new Option<string?>(
            "--follow-ref",
            description: "FormKey of actor to follow (for followto packages)");
        var locationRefOption = new Option<string?>(
            "--location-ref",
            description: "FormKey of location (for say packages)");

        var cmd = new Command("add-package", "Add an AI package to a plugin")
        {
            pluginArg,
            editorIdArg,
            typeOption,
            radiusOption,
            locationOption,
            bedRefOption,
            furnitureRefOption,
            targetRefOption,
            markerRefOption,
            startHourOption,
            durationOption,
            itemRefOption,
            fleeFromOption,
            distanceOption,
            destinationRefOption,
            weaponRefOption,
            spellRefOption,
            doorRefOption,
            objectRefOption,
            escortRefOption,
            topicRefOption,
            shoutRefOption,
            followRefOption,
            locationRefOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var editorId = context.ParseResult.GetValueForArgument(editorIdArg);
            var type = context.ParseResult.GetValueForOption(typeOption)!;
            var radius = context.ParseResult.GetValueForOption(radiusOption);
            var location = context.ParseResult.GetValueForOption(locationOption);
            var bedRef = context.ParseResult.GetValueForOption(bedRefOption);
            var furnitureRef = context.ParseResult.GetValueForOption(furnitureRefOption);
            var targetRef = context.ParseResult.GetValueForOption(targetRefOption);
            var markerRef = context.ParseResult.GetValueForOption(markerRefOption);
            var startHour = context.ParseResult.GetValueForOption(startHourOption);
            var duration = context.ParseResult.GetValueForOption(durationOption);
            var itemRef = context.ParseResult.GetValueForOption(itemRefOption);
            var fleeFrom = context.ParseResult.GetValueForOption(fleeFromOption);
            var distance = context.ParseResult.GetValueForOption(distanceOption);
            var destinationRef = context.ParseResult.GetValueForOption(destinationRefOption);
            var weaponRef = context.ParseResult.GetValueForOption(weaponRefOption);
            var spellRef = context.ParseResult.GetValueForOption(spellRefOption);
            var doorRef = context.ParseResult.GetValueForOption(doorRefOption);
            var objectRef = context.ParseResult.GetValueForOption(objectRefOption);
            var escortRef = context.ParseResult.GetValueForOption(escortRefOption);
            var topicRef = context.ParseResult.GetValueForOption(topicRefOption);
            var shoutRef = context.ParseResult.GetValueForOption(shoutRefOption);
            var followRef = context.ParseResult.GetValueForOption(followRefOption);
            var locationRef = context.ParseResult.GetValueForOption(locationRefOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            // Build options dictionary
            var options = new Dictionary<string, object>
            {
                ["radius"] = radius,
                ["startHour"] = startHour,
                ["duration"] = duration,
                ["distance"] = distance
            };

            if (!string.IsNullOrEmpty(location))
                options["location"] = location;
            if (!string.IsNullOrEmpty(bedRef))
                options["bedRef"] = bedRef;
            if (!string.IsNullOrEmpty(furnitureRef))
                options["furnitureRef"] = furnitureRef;
            if (!string.IsNullOrEmpty(targetRef))
                options["targetRef"] = targetRef;
            if (!string.IsNullOrEmpty(markerRef))
                options["markerRef"] = markerRef;
            if (!string.IsNullOrEmpty(itemRef))
                options["itemRef"] = itemRef;
            if (!string.IsNullOrEmpty(fleeFrom))
                options["fleeFrom"] = fleeFrom;
            if (!string.IsNullOrEmpty(destinationRef))
                options["destinationRef"] = destinationRef;
            if (!string.IsNullOrEmpty(weaponRef))
                options["weaponRef"] = weaponRef;
            if (!string.IsNullOrEmpty(spellRef))
                options["spellRef"] = spellRef;
            if (!string.IsNullOrEmpty(doorRef))
                options["doorRef"] = doorRef;
            if (!string.IsNullOrEmpty(objectRef))
                options["objectRef"] = objectRef;
            if (!string.IsNullOrEmpty(escortRef))
                options["escortRef"] = escortRef;
            if (!string.IsNullOrEmpty(topicRef))
                options["topicRef"] = topicRef;
            if (!string.IsNullOrEmpty(shoutRef))
                options["shoutRef"] = shoutRef;
            if (!string.IsNullOrEmpty(followRef))
                options["followRef"] = followRef;
            if (!string.IsNullOrEmpty(locationRef))
                options["locationRef"] = locationRef;

            var result = service.AddPackage(plugin, editorId, type, options);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Package added: {editorId} (FormKey: {result.Value})");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.Suggestions != null && result.Suggestions.Count > 0)
                {
                    Console.Error.WriteLine("\nSuggestions:");
                    foreach (var suggestion in result.Suggestions)
                    {
                        Console.Error.WriteLine($"  - {suggestion}");
                    }
                }
                Environment.ExitCode = 1;
            }
        });

        return cmd;
    }

    private static Command CreateAttachPackageCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var npcOption = new Option<string>(
            aliases: new[] { "--npc", "-n" },
            description: "Editor ID of the NPC"
        ) { IsRequired = true };
        var packageOption = new Option<string>(
            aliases: new[] { "--package", "-p" },
            description: "Editor ID of the package to attach"
        ) { IsRequired = true };

        var cmd = new Command("attach-package", "Attach a package to an NPC")
        {
            pluginArg,
            npcOption,
            packageOption
        };

        cmd.SetHandler((plugin, npc, package, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.AttachPackageToNpc(plugin, npc, package);

            if (json)
            {
                Console.WriteLine(result.ToJson(true));
            }
            else if (result.Success)
            {
                Console.WriteLine($"Package '{package}' attached to NPC '{npc}'");
            }
            else
            {
                Console.Error.WriteLine($"Error: {result.Error}");
                if (result.Suggestions != null && result.Suggestions.Count > 0)
                {
                    Console.Error.WriteLine("\nSuggestions:");
                    foreach (var suggestion in result.Suggestions)
                    {
                        Console.Error.WriteLine($"  - {suggestion}");
                    }
                }
            }
        },
        pluginArg, npcOption, packageOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static Command CreateRemoveRecordCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var editorIdArg = new Argument<string>("editorId", "Editor ID of the record to remove");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("remove-record", "Remove a record from a plugin by EditorID")
        {
            pluginArg, editorIdArg, dryRunOption
        };

        cmd.SetHandler((plugin, editorId, dryRun, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;

            if (dryRun)
            {
                // Verify record exists without removing
                var findResult = service.FindRecords(null, editorId, null, plugin, null, false);
                if (findResult.Success && findResult.Value != null && findResult.Value.Count > 0)
                {
                    var record = findResult.Value[0];
                    if (json)
                    {
                        Console.WriteLine(new
                        {
                            success = true,
                            result = new { editorId = record.EditorId, formKey = record.FormKey, type = record.RecordType, dryRun = true }
                        }.ToJson());
                    }
                    else
                    {
                        Console.WriteLine($"Would remove: [{record.RecordType}] {record.EditorId} ({record.FormKey}) [DRY RUN - not saved]");
                    }
                }
                else
                {
                    OutputError($"Record not found: {editorId}", json);
                }
                return;
            }

            var removeResult = service.RemoveRecord(mod, editorId);
            if (!removeResult.Success) { OutputError(removeResult.Error!, json, removeResult.Suggestions); return; }

            var saveResult = service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                    Console.WriteLine(new { success = true, result = new { editorId, removed = true } }.ToJson());
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Removed record: {editorId}");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        }, pluginArg, editorIdArg, dryRunOption, _jsonOption, _verboseOption);

        return cmd;
    }

    private static bool TryParseTriple(string? s, out float x, out float y, out float z)
    {
        x = y = z = 0f;
        if (string.IsNullOrWhiteSpace(s)) return true;
        var parts = s.Split(',');
        if (parts.Length != 3) return false;
        return float.TryParse(parts[0].Trim(), out x)
            && float.TryParse(parts[1].Trim(), out y)
            && float.TryParse(parts[2].Trim(), out z);
    }

    private static Command CreateOverridePlacedObjectCommand()
    {
        var refOption = new Option<string>("--ref", "FormKey of the existing reference to override (e.g. '078039:Skyrim.esm')") { IsRequired = true };
        var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output patch plugin (appended to if it exists)") { IsRequired = true };
        var dataFolderOption = new Option<string>("--data-folder", "Data folder containing plugins.txt and the plugins") { IsRequired = true };
        var baseOption = new Option<string>("--base", "New base object FormKey (optional)");
        var scaleOption = new Option<string>("--scale", "New scale (optional)");
        var posOption = new Option<string>("--pos", "New position as x,y,z (optional)");
        var rotOption = new Option<string>("--rot", "New rotation as x,y,z (optional)");
        var lightOption = new Option<bool>("--light", "Flag the output plugin as ESL");

        var cmd = new Command("override-placed-object", "Override an existing placed reference in a patch (bakes in the winning version)")
        {
            refOption, outputOption, dataFolderOption, baseOption, scaleOption, posOption, rotOption, lightOption
        };

        cmd.SetHandler((context) =>
        {
            var refKey = context.ParseResult.GetValueForOption(refOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var dataFolder = context.ParseResult.GetValueForOption(dataFolderOption)!;
            var baseObj = context.ParseResult.GetValueForOption(baseOption);
            var scaleStr = context.ParseResult.GetValueForOption(scaleOption);
            var pos = context.ParseResult.GetValueForOption(posOption);
            var rot = context.ParseResult.GetValueForOption(rotOption);
            var light = context.ParseResult.GetValueForOption(lightOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            float? scale = null;
            if (!string.IsNullOrWhiteSpace(scaleStr))
            {
                if (!float.TryParse(scaleStr, out var s)) { OutputError("Invalid --scale", json); return; }
                scale = s;
            }

            (float, float, float)? posTuple = null;
            if (!string.IsNullOrWhiteSpace(pos))
            {
                if (!TryParseTriple(pos, out var px, out var py, out var pz)) { OutputError("Invalid --pos, expected 'x,y,z'", json); return; }
                posTuple = (px, py, pz);
            }

            (float, float, float)? rotTuple = null;
            if (!string.IsNullOrWhiteSpace(rot))
            {
                if (!TryParseTriple(rot, out var rx, out var ry, out var rz)) { OutputError("Invalid --rot, expected 'x,y,z'", json); return; }
                rotTuple = (rx, ry, rz);
            }

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.OverridePlacedObject(dataFolder, refKey, output, baseObj, scale, posTuple, rotTuple, light);

            if (!result.Success) { OutputError(result.Error!, json, result.Suggestions); return; }

            if (json) Console.WriteLine(new { success = true, result = new { path = result.Value } }.ToJson());
            else Console.WriteLine($"Overrode {refKey} in: {result.Value}");
        });

        return cmd;
    }

    private static Command CreateRemovePlacedObjectCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file to edit");
        var formIdOption = new Option<string>("--form-id", "FormKey of the placed object to remove") { IsRequired = true };

        var cmd = new Command("remove-placed-object", "Remove a placed object reference from a plugin")
        {
            pluginArg, formIdOption
        };

        cmd.SetHandler((context) =>
        {
            var plugin = context.ParseResult.GetValueForArgument(pluginArg);
            var formId = context.ParseResult.GetValueForOption(formIdOption)!;
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.RemovePlacedObject(plugin, formId);

            if (!result.Success) { OutputError(result.Error!, json, result.Suggestions); return; }

            if (json) Console.WriteLine(new { success = true, result = new { path = result.Value } }.ToJson());
            else Console.WriteLine($"Removed {formId} from: {result.Value}");
        });

        return cmd;
    }

    private static Command CreateAddPlacedObjectCommand()
    {
        var cellOption = new Option<string>("--cell", "FormKey of the cell to place into (e.g. 'Skyrim.esm:0x0165B5')") { IsRequired = true };
        var baseOption = new Option<string>("--base", "FormKey of the base object to place (e.g. 'Embers XD.esp:0x18112A')") { IsRequired = true };
        var outputOption = new Option<string>(new[] { "-o", "--output" }, "Name of the output patch plugin") { IsRequired = true };
        var dataFolderOption = new Option<string>("--data-folder", "Data folder containing the masters") { IsRequired = true };
        var posOption = new Option<string>("--pos", "Position as x,y,z") { IsRequired = true };
        var rotOption = new Option<string>("--rot", "Rotation in degrees as x,y,z (default 0,0,0)");
        var scaleOption = new Option<float>("--scale", () => 1.0f, "Scale (default 1.0)");
        var lightOption = new Option<bool>("--light", "Flag the output plugin as ESL (light master)");

        var cmd = new Command("add-placed-object", "Add a new placed object reference into an existing cell")
        {
            cellOption, baseOption, outputOption, dataFolderOption, posOption, rotOption, scaleOption, lightOption
        };

        cmd.SetHandler((context) =>
        {
            var cell = context.ParseResult.GetValueForOption(cellOption)!;
            var baseObj = context.ParseResult.GetValueForOption(baseOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption)!;
            var dataFolder = context.ParseResult.GetValueForOption(dataFolderOption)!;
            var pos = context.ParseResult.GetValueForOption(posOption)!;
            var rot = context.ParseResult.GetValueForOption(rotOption);
            var scale = context.ParseResult.GetValueForOption(scaleOption);
            var light = context.ParseResult.GetValueForOption(lightOption);
            var json = context.ParseResult.GetValueForOption(_jsonOption);
            var verbose = context.ParseResult.GetValueForOption(_verboseOption);

            static bool TryParseTriple(string? s, out float x, out float y, out float z)
            {
                x = y = z = 0f;
                if (string.IsNullOrWhiteSpace(s)) return true;
                var parts = s.Split(',');
                if (parts.Length != 3) return false;
                return float.TryParse(parts[0].Trim(), out x)
                    && float.TryParse(parts[1].Trim(), out y)
                    && float.TryParse(parts[2].Trim(), out z);
            }

            if (!TryParseTriple(pos, out var px, out var py, out var pz))
            {
                OutputError("Invalid --pos, expected 'x,y,z'", json);
                return;
            }

            if (!TryParseTriple(rot, out var rx, out var ry, out var rz))
            {
                OutputError("Invalid --rot, expected 'x,y,z'", json);
                return;
            }

            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var result = service.AddPlacedObject(dataFolder, cell, baseObj, output, px, py, pz, rx, ry, rz, scale, light);

            if (!result.Success)
            {
                OutputError(result.Error!, json, result.Suggestions);
                return;
            }

            if (json)
            {
                Console.WriteLine(new { success = true, result = new { path = result.Value } }.ToJson());
            }
            else
            {
                Console.WriteLine($"Added placed object to: {result.Value}");
            }
        });

        return cmd;
    }

    private static Command CreateCloneRecordCommand()
    {
        var pluginArg = new Argument<string>("plugin", "Path to the plugin file");
        var sourceArg = new Argument<string>("source", "Editor ID of the record to clone");
        var newEditorIdArg = new Argument<string>("newEditorId", "Editor ID for the cloned record");
        var dryRunOption = new Option<bool>("--dry-run", "Preview changes without saving");

        var cmd = new Command("clone-record", "Clone a record with a new EditorID and FormKey")
        {
            pluginArg, sourceArg, newEditorIdArg, dryRunOption
        };

        cmd.SetHandler((plugin, source, newEditorId, dryRun, json, verbose) =>
        {
            var logger = CreateLogger(json, verbose);
            var service = new PluginService(logger);

            var loadResult = service.LoadPluginForEdit(plugin);
            if (!loadResult.Success) { OutputError(loadResult.Error!, json); return; }

            var mod = loadResult.Value!;

            if (dryRun)
            {
                var findResult = service.FindRecords(null, source, null, plugin, null, false);
                if (findResult.Success && findResult.Value != null && findResult.Value.Count > 0)
                {
                    var record = findResult.Value[0];
                    if (json)
                    {
                        Console.WriteLine(new
                        {
                            success = true,
                            result = new { sourceEditorId = source, newEditorId, type = record.RecordType, dryRun = true }
                        }.ToJson());
                    }
                    else
                    {
                        Console.WriteLine($"Would clone: [{record.RecordType}] {source} -> {newEditorId} [DRY RUN - not saved]");
                    }
                }
                else
                {
                    OutputError($"Source record not found: {source}", json);
                }
                return;
            }

            var cloneResult = service.CloneRecord(mod, source, newEditorId);
            if (!cloneResult.Success) { OutputError(cloneResult.Error!, json, cloneResult.Suggestions); return; }

            var saveResult = service.SavePlugin(mod, plugin);

            if (json)
            {
                if (saveResult.Success)
                {
                    Console.WriteLine(new
                    {
                        success = true,
                        result = new
                        {
                            sourceEditorId = source,
                            editorId = cloneResult.Value!.EditorId,
                            formKey = cloneResult.Value.FormKey,
                            type = cloneResult.Value.RecordType
                        }
                    }.ToJson());
                }
                else
                {
                    Console.WriteLine(new { success = false, error = saveResult.Error }.ToJson());
                    Environment.ExitCode = 1;
                }
            }
            else if (saveResult.Success)
            {
                Console.WriteLine($"Cloned: {source} -> {newEditorId} ({cloneResult.Value!.FormKey})");
            }
            else
            {
                Console.Error.WriteLine($"Error: {saveResult.Error}");
                Environment.ExitCode = 1;
            }
        }, pluginArg, sourceArg, newEditorIdArg, dryRunOption, _jsonOption, _verboseOption);

        return cmd;
    }
}

// Extension for anonymous type JSON
internal static class JsonExtensions
{
    public static string ToJson(this object obj)
    {
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
