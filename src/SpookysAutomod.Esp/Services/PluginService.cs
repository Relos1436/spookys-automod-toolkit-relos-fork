using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Esp.Builders;
using System.Reflection;

namespace SpookysAutomod.Esp.Services;

/// <summary>
/// Service for creating, loading, and saving ESP/ESM/ESL plugin files.
/// </summary>
public class PluginService
{
    private readonly IModLogger _logger;

    public PluginService(IModLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a new empty plugin.
    /// </summary>
    public Result<string> CreatePlugin(
        string name,
        string outputPath,
        bool isLight = false,
        string? author = null,
        string? description = null)
    {
        try
        {
            // Ensure .esp extension
            if (!name.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                name += ".esp";
            }

            var modKey = ModKey.FromFileName(name);
            var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);

            // Set header flags for light plugin (ESL flag = 0x200)
            if (isLight)
            {
                mod.ModHeader.Flags |= (SkyrimModHeader.HeaderFlag)0x200;
            }

            if (!string.IsNullOrEmpty(author))
            {
                mod.ModHeader.Author = author;
            }

            if (!string.IsNullOrEmpty(description))
            {
                mod.ModHeader.Description = description;
            }

            // Ensure output directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var fullPath = Path.IsPathRooted(outputPath)
                ? Path.Combine(outputPath, name)
                : Path.Combine(Directory.GetCurrentDirectory(), outputPath, name);

            mod.WriteToBinary(fullPath);

            _logger.Info($"Created plugin: {fullPath}");
            return Result<string>.Ok(fullPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Failed to create plugin: {ex.Message}",
                ex.StackTrace,
                new List<string>
                {
                    "Ensure the output path is writable",
                    "Check that the plugin name contains only valid characters"
                });
        }
    }

    /// <summary>
    /// Load an existing plugin for reading.
    /// </summary>
    public Result<ISkyrimModGetter> LoadPluginReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Result<ISkyrimModGetter>.Fail(
                    $"Plugin not found: {path}",
                    suggestions: new List<string>
                    {
                        "Check the file path is correct",
                        "Use an absolute path if relative path fails"
                    });
            }

            var mod = SkyrimMod.CreateFromBinaryOverlay(path, SkyrimRelease.SkyrimSE);
            _logger.Debug($"Loaded plugin (read-only): {path}");
            return Result<ISkyrimModGetter>.Ok(mod);
        }
        catch (Exception ex)
        {
            return Result<ISkyrimModGetter>.Fail(
                $"Failed to load plugin: {ex.Message}",
                ex.StackTrace,
                new List<string>
                {
                    "Ensure the file is a valid Skyrim plugin",
                    "Check if the file is corrupted",
                    "Verify the file is not locked by another process"
                });
        }
    }

    /// <summary>
    /// Load an existing plugin for editing.
    /// </summary>
    public Result<SkyrimMod> LoadPluginForEdit(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Result<SkyrimMod>.Fail(
                    $"Plugin not found: {path}",
                    suggestions: new List<string>
                    {
                        "Check the file path is correct",
                        "Use an absolute path if relative path fails"
                    });
            }

            // Use import mask to properly initialize FormKey allocation
            var mod = SkyrimMod.CreateFromBinary(
                path,
                SkyrimRelease.SkyrimSE,
                new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters
                {
                    // This ensures the FormKey allocator is properly initialized
                });

            // If the mod is empty or has low FormIDs, set a proper starting point
            // Skyrim mods should allocate FormIDs starting from 0x800
            if (mod.ModHeader.Stats.NextFormID < 0x800)
            {
                mod.ModHeader.Stats.NextFormID = 0x800;
            }

            _logger.Debug($"Loaded plugin (editable): {path}");
            return Result<SkyrimMod>.Ok(mod);
        }
        catch (Exception ex)
        {
            return Result<SkyrimMod>.Fail(
                $"Failed to load plugin: {ex.Message}",
                ex.StackTrace,
                new List<string>
                {
                    "Ensure the file is a valid Skyrim plugin",
                    "Check if the file is corrupted",
                    "Verify the file is not locked by another process"
                });
        }
    }

    /// <summary>
    /// Save a plugin to disk.
    /// </summary>
    public Result SavePlugin(SkyrimMod mod, string? outputPath = null)
    {
        try
        {
            var path = outputPath ?? Path.Combine(Directory.GetCurrentDirectory(), mod.ModKey.FileName);
            mod.WriteToBinary(path);
            _logger.Info($"Saved plugin: {path}");
            return Result.Ok($"Plugin saved to: {path}");
        }
        catch (Exception ex)
        {
            return Result.Fail(
                $"Failed to save plugin: {ex.Message}",
                ex.StackTrace,
                new List<string>
                {
                    "Ensure the output path is writable",
                    "Check that no other process has the file locked"
                });
        }
    }

    /// <summary>
    /// Get information about a plugin.
    /// </summary>
    public Result<PluginInfo> GetPluginInfo(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Result<PluginInfo>.Fail($"Plugin not found: {path}");
            }

            var mod = SkyrimMod.CreateFromBinaryOverlay(path, SkyrimRelease.SkyrimSE);
            var fileInfo = new FileInfo(path);

            var info = new PluginInfo
            {
                FileName = mod.ModKey.FileName,
                FilePath = path,
                Author = mod.ModHeader.Author,
                Description = mod.ModHeader.Description,
                IsLight = mod.ModHeader.Flags.HasFlag((SkyrimModHeader.HeaderFlag)0x200),
                IsMaster = mod.ModKey.Type == ModType.Master,
                FileSize = fileInfo.Length
            };

            // Get master files
            foreach (var master in mod.ModHeader.MasterReferences)
            {
                info.MasterFiles.Add(master.Master.FileName);
            }

            // Count records by type
            info.RecordCounts["Quests"] = mod.Quests.Count;
            info.RecordCounts["Spells"] = mod.Spells.Count;
            info.RecordCounts["Globals"] = mod.Globals.Count;
            info.RecordCounts["NPCs"] = mod.Npcs.Count;
            info.RecordCounts["Weapons"] = mod.Weapons.Count;
            info.RecordCounts["Armors"] = mod.Armors.Count;
            info.RecordCounts["Books"] = mod.Books.Count;
            info.RecordCounts["Perks"] = mod.Perks.Count;
            info.RecordCounts["Factions"] = mod.Factions.Count;
            info.RecordCounts["MiscItems"] = mod.MiscItems.Count;
            info.RecordCounts["LeveledItems"] = mod.LeveledItems.Count;
            info.RecordCounts["FormLists"] = mod.FormLists.Count;
            info.RecordCounts["EncounterZones"] = mod.EncounterZones.Count;
            info.RecordCounts["Locations"] = mod.Locations.Count;
            info.RecordCounts["Outfits"] = mod.Outfits.Count;

            info.TotalRecords = info.RecordCounts.Values.Sum();

            return Result<PluginInfo>.Ok(info);
        }
        catch (Exception ex)
        {
            return Result<PluginInfo>.Fail(
                $"Failed to read plugin info: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Generate a SEQ file for quests that start enabled.
    /// </summary>
    public Result<string> GenerateSeqFile(string pluginPath, string outputDir)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result<string>.Fail($"Plugin not found: {pluginPath}");
            }

            var mod = SkyrimMod.CreateFromBinaryOverlay(pluginPath, SkyrimRelease.SkyrimSE);
            var startEnabledQuests = new List<uint>();

            foreach (var quest in mod.Quests)
            {
                if (quest.Flags.HasFlag(Quest.Flag.StartGameEnabled))
                {
                    startEnabledQuests.Add(quest.FormKey.ID);
                }
            }

            if (startEnabledQuests.Count == 0)
            {
                return Result<string>.Fail(
                    "No start-enabled quests found",
                    suggestions: new List<string>
                    {
                        "Add a quest with StartGameEnabled flag",
                        "SEQ files are only needed for quests that start on game load"
                    });
            }

            // Ensure output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var seqFileName = Path.GetFileNameWithoutExtension(mod.ModKey.FileName) + ".seq";
            var seqPath = Path.Combine(outputDir, seqFileName);

            // Write SEQ file (simple format: count + FormIDs)
            using var writer = new BinaryWriter(File.Create(seqPath));
            writer.Write((uint)startEnabledQuests.Count);
            foreach (var formId in startEnabledQuests)
            {
                writer.Write(formId);
            }

            _logger.Info($"Generated SEQ file with {startEnabledQuests.Count} quest(s): {seqPath}");
            return Result<string>.Ok(seqPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Failed to generate SEQ file: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// View detailed information about a record
    /// </summary>
    public Result<RecordInfo> ViewRecord(
        string pluginPath,
        string? editorId,
        string? formId,
        string? recordType,
        bool includeRaw = false)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result<RecordInfo>.Fail(
                    $"Plugin not found: {pluginPath}",
                    suggestions: new List<string>
                    {
                        "Check the file path is correct",
                        "Use an absolute path if relative path fails"
                    });
            }

            var mod = SkyrimMod.CreateFromBinaryOverlay(pluginPath, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? record = null;

            if (!string.IsNullOrEmpty(formId))
            {
                var findResult = FindRecordByFormKey(mod, formId);
                if (!findResult.Success)
                {
                    return Result<RecordInfo>.Fail(findResult.Error ?? "Record not found", findResult.ErrorContext);
                }
                record = findResult.Value;
            }
            else if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(recordType))
            {
                var findResult = FindRecordByEditorId(mod, editorId, recordType);
                if (!findResult.Success)
                {
                    return Result<RecordInfo>.Fail(findResult.Error ?? "Record not found", findResult.ErrorContext);
                }
                record = findResult.Value;
            }
            else
            {
                return Result<RecordInfo>.Fail(
                    "Must provide either FormID or both EditorID and RecordType",
                    suggestions: new List<string>
                    {
                        "Use --form-id for FormKey-based lookup",
                        "Use --editor-id and --type for EditorID-based lookup"
                    });
            }

            if (record == null)
            {
                return Result<RecordInfo>.Fail(
                    $"Record not found: {editorId ?? formId}",
                    suggestions: new List<string>
                    {
                        "Use 'esp info' to see available records",
                        "Check spelling of EditorID",
                        "Verify FormID is correct"
                    });
            }

            var recordInfo = new RecordInfo
            {
                EditorId = record.EditorID ?? string.Empty,
                FormKey = record.FormKey.ToString(),
                RecordType = record.GetType().Name.Replace("Getter", "").Replace("ReadOnly", "")
            };

            var propsResult = ExtractRecordProperties(record, includeRaw);
            if (!propsResult.Success)
            {
                return Result<RecordInfo>.Fail(propsResult.Error ?? "Failed to extract properties", propsResult.ErrorContext);
            }
            recordInfo.Properties = propsResult.Value ?? new Dictionary<string, object?>();

            var conditionsResult = ExtractConditions(record);
            if (conditionsResult.Success && conditionsResult.Value != null && conditionsResult.Value.Count > 0)
            {
                recordInfo.Conditions = conditionsResult.Value;
            }

            return Result<RecordInfo>.Ok(recordInfo);
        }
        catch (Exception ex)
        {
            return Result<RecordInfo>.Fail(
                $"Failed to view record: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Add a package (AI behavior) to a plugin.
    /// </summary>
    public Result<string> AddPackage(
        string pluginPath,
        string editorId,
        string packageType,
        Dictionary<string, object>? options = null)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result<string>.Fail($"Plugin not found: {pluginPath}");
            }

            var mod = SkyrimMod.CreateFromBinary(
                pluginPath,
                SkyrimRelease.SkyrimSE,
                new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters());

            // Ensure proper FormID allocation
            if (mod.ModHeader.Stats.NextFormID < 0x800)
            {
                mod.ModHeader.Stats.NextFormID = 0x800;
            }

            var builder = new PackageBuilder(mod, editorId);

            // Configure package based on type
            switch (packageType.ToLowerInvariant())
            {
                case "sandbox":
                    var radius = options?.GetValueOrDefault("radius", 500) ?? 500;
                    builder.AsSandbox(Convert.ToUInt16(radius));
                    break;

                case "travel":
                    if (options?.ContainsKey("destination") == true)
                    {
                        var destStr = options["destination"].ToString();
                        if (!string.IsNullOrEmpty(destStr) && FormKey.TryFactory(destStr, out var destFormKey))
                        {
                            builder.AsTravel(destFormKey);
                        }
                    }
                    break;

                case "sleep":
                    var startHour = Convert.ToByte(options?.GetValueOrDefault("startHour", 22) ?? 22);
                    var duration = Convert.ToByte(options?.GetValueOrDefault("duration", 8) ?? 8);

                    if (options?.ContainsKey("bedRef") == true)
                    {
                        var bedRefStr = options["bedRef"]?.ToString();
                        if (!string.IsNullOrEmpty(bedRefStr) && FormKey.TryFactory(bedRefStr, out var bedFormKey))
                        {
                            builder.AsSleep(bedFormKey, startHour, duration);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid bed reference FormKey: {bedRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Sleep packages require a valid bed reference (bedRef)");
                    }
                    break;

                case "eat":
                    var eatStartHour = Convert.ToByte(options?.GetValueOrDefault("startHour", 12) ?? 12);
                    var eatDuration = Convert.ToByte(options?.GetValueOrDefault("duration", 2) ?? 2);

                    if (options?.ContainsKey("furnitureRef") == true)
                    {
                        var furnitureRefStr = options["furnitureRef"]?.ToString();
                        if (!string.IsNullOrEmpty(furnitureRefStr) && FormKey.TryFactory(furnitureRefStr, out var furnitureFormKey))
                        {
                            builder.AsEat(furnitureFormKey, eatStartHour, eatDuration);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid furniture reference FormKey: {furnitureRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Eat packages require a valid furniture reference (furnitureRef)");
                    }
                    break;

                case "follow":
                    var followDistance = Convert.ToUInt16(options?.GetValueOrDefault("followDistance", 200) ?? 200);

                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var targetRefStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(targetRefStr) && FormKey.TryFactory(targetRefStr, out var targetFormKey))
                        {
                            builder.AsFollow(targetFormKey, followDistance);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid target reference FormKey: {targetRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Follow packages require a valid target reference (targetRef)");
                    }
                    break;

                case "guard":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var markerRefStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(markerRefStr) && FormKey.TryFactory(markerRefStr, out var markerFormKey))
                        {
                            builder.AsGuard(markerFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid marker reference FormKey: {markerRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Guard packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "useitemat":
                case "activate":
                case "use":
                    if (options?.ContainsKey("itemRef") == true)
                    {
                        var itemRefStr = options["itemRef"]?.ToString();
                        if (!string.IsNullOrEmpty(itemRefStr) && FormKey.TryFactory(itemRefStr, out var itemFormKey))
                        {
                            builder.AsUseItemAt(itemFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid item reference FormKey: {itemRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("UseItemAt packages require a valid item reference (itemRef)");
                    }
                    break;

                case "sit":
                    if (options?.ContainsKey("furnitureRef") == true)
                    {
                        var sitFurnitureStr = options["furnitureRef"]?.ToString();
                        if (!string.IsNullOrEmpty(sitFurnitureStr) && FormKey.TryFactory(sitFurnitureStr, out var sitFurnitureFormKey))
                        {
                            builder.AsSit(sitFurnitureFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid furniture reference FormKey: {sitFurnitureStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Sit packages require a valid furniture reference (furnitureRef)");
                    }
                    break;

                case "useidlemarker":
                case "idle":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var idleMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(idleMarkerStr) && FormKey.TryFactory(idleMarkerStr, out var idleMarkerFormKey))
                        {
                            builder.AsUseIdleMarker(idleMarkerFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid idle marker reference FormKey: {idleMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("UseIdleMarker packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "flee":
                    var fleeDistance = Convert.ToUInt16(options?.GetValueOrDefault("distance", 1000) ?? 1000);

                    if (options?.ContainsKey("fleeFrom") == true)
                    {
                        var fleeFromStr = options["fleeFrom"]?.ToString();
                        if (!string.IsNullOrEmpty(fleeFromStr) && FormKey.TryFactory(fleeFromStr, out var fleeFromFormKey))
                        {
                            builder.AsFlee(fleeFromFormKey, fleeDistance);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid flee-from reference FormKey: {fleeFromStr}");
                        }
                    }
                    else
                    {
                        // Flee from combat (no specific target)
                        builder.AsFlee(null, fleeDistance);
                    }
                    break;

                case "accompany":
                case "escort":
                    if (options?.ContainsKey("targetRef") == true && options?.ContainsKey("destinationRef") == true)
                    {
                        var accompanyTargetStr = options["targetRef"]?.ToString();
                        var accompanyDestStr = options["destinationRef"]?.ToString();

                        if (!string.IsNullOrEmpty(accompanyTargetStr) && FormKey.TryFactory(accompanyTargetStr, out var accompanyTargetFormKey) &&
                            !string.IsNullOrEmpty(accompanyDestStr) && FormKey.TryFactory(accompanyDestStr, out var accompanyDestFormKey))
                        {
                            builder.AsAccompany(accompanyTargetFormKey, accompanyDestFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail("Invalid FormKey for target or destination");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Accompany packages require both target reference (targetRef) and destination reference (destinationRef)");
                    }
                    break;

                case "castmagic":
                case "cast":
                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var castTargetStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(castTargetStr) && FormKey.TryFactory(castTargetStr, out var castTargetFormKey))
                        {
                            builder.AsCastMagic(castTargetFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid cast target FormKey: {castTargetStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("CastMagic packages require a valid target reference (targetRef)");
                    }
                    break;

                case "dialogue":
                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var dialogueTargetStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(dialogueTargetStr) && FormKey.TryFactory(dialogueTargetStr, out var dialogueTargetFormKey))
                        {
                            builder.AsDialogue(dialogueTargetFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid dialogue target FormKey: {dialogueTargetStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Dialogue packages require a valid target reference (targetRef)");
                    }
                    break;

                case "find":
                case "search":
                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var findTargetStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(findTargetStr) && FormKey.TryFactory(findTargetStr, out var findTargetFormKey))
                        {
                            builder.AsFind(findTargetFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid find target FormKey: {findTargetStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Find packages require a valid target reference (targetRef)");
                    }
                    break;

                case "ambush":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var ambushMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(ambushMarkerStr) && FormKey.TryFactory(ambushMarkerStr, out var ambushMarkerFormKey))
                        {
                            builder.AsAmbush(ambushMarkerFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid ambush marker FormKey: {ambushMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Ambush packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "patrol":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var patrolMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(patrolMarkerStr) && FormKey.TryFactory(patrolMarkerStr, out var patrolMarkerFormKey))
                        {
                            builder.AsPatrol(patrolMarkerFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid patrol marker FormKey: {patrolMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Patrol packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "wander":
                    var wanderRadius = Convert.ToUInt16(options?.GetValueOrDefault("radius", 1000) ?? 1000);
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var wanderMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(wanderMarkerStr) && FormKey.TryFactory(wanderMarkerStr, out var wanderMarkerFormKey))
                        {
                            builder.AsWander(wanderMarkerFormKey, wanderRadius);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid wander marker FormKey: {wanderMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Wander packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "wait":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var waitMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(waitMarkerStr) && FormKey.TryFactory(waitMarkerStr, out var waitMarkerFormKey))
                        {
                            builder.AsWait(waitMarkerFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid wait marker FormKey: {waitMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Wait packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "relax":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var relaxMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(relaxMarkerStr) && FormKey.TryFactory(relaxMarkerStr, out var relaxMarkerFormKey))
                        {
                            builder.AsRelax(relaxMarkerFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid relax marker FormKey: {relaxMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Relax packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "forcegreet":
                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var forceGreetTargetStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(forceGreetTargetStr) && FormKey.TryFactory(forceGreetTargetStr, out var forceGreetTargetFormKey))
                        {
                            builder.AsForceGreet(forceGreetTargetFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid force greet target FormKey: {forceGreetTargetStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("ForceGreet packages require a valid target reference (targetRef)");
                    }
                    break;

                case "greet":
                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var greetTargetStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(greetTargetStr) && FormKey.TryFactory(greetTargetStr, out var greetTargetFormKey))
                        {
                            builder.AsGreet(greetTargetFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid greet target FormKey: {greetTargetStr}");
                        }
                    }
                    else
                    {
                        // Greet with no specific target
                        builder.AsGreet();
                    }
                    break;

                case "useweapon":
                    if (options?.ContainsKey("weaponRef") == true)
                    {
                        var weaponRefStr = options["weaponRef"]?.ToString();
                        if (!string.IsNullOrEmpty(weaponRefStr) && FormKey.TryFactory(weaponRefStr, out var weaponFormKey))
                        {
                            FormKey? weaponTarget = null;
                            if (options?.ContainsKey("targetRef") == true)
                            {
                                var targetStr = options["targetRef"]?.ToString();
                                if (!string.IsNullOrEmpty(targetStr) && FormKey.TryFactory(targetStr, out var targetFormKey))
                                {
                                    weaponTarget = targetFormKey;
                                }
                            }
                            builder.AsUseWeapon(weaponFormKey, weaponTarget);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid weapon FormKey: {weaponRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("UseWeapon packages require a valid weapon reference (weaponRef)");
                    }
                    break;

                case "usemagic":
                    if (options?.ContainsKey("spellRef") == true)
                    {
                        var spellRefStr = options["spellRef"]?.ToString();
                        if (!string.IsNullOrEmpty(spellRefStr) && FormKey.TryFactory(spellRefStr, out var spellFormKey))
                        {
                            FormKey? magicTarget = null;
                            if (options?.ContainsKey("targetRef") == true)
                            {
                                var targetStr = options["targetRef"]?.ToString();
                                if (!string.IsNullOrEmpty(targetStr) && FormKey.TryFactory(targetStr, out var targetFormKey))
                                {
                                    magicTarget = targetFormKey;
                                }
                            }
                            builder.AsUseMagic(spellFormKey, magicTarget);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid spell FormKey: {spellRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("UseMagic packages require a valid spell reference (spellRef)");
                    }
                    break;

                case "lockdoors":
                    if (options?.ContainsKey("doorRef") == true)
                    {
                        var lockDoorStr = options["doorRef"]?.ToString();
                        if (!string.IsNullOrEmpty(lockDoorStr) && FormKey.TryFactory(lockDoorStr, out var lockDoorFormKey))
                        {
                            builder.AsLockDoors(lockDoorFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid door FormKey: {lockDoorStr}");
                        }
                    }
                    else
                    {
                        // Lock all owned doors
                        builder.AsLockDoors();
                    }
                    break;

                case "unlockdoors":
                    if (options?.ContainsKey("doorRef") == true)
                    {
                        var unlockDoorStr = options["doorRef"]?.ToString();
                        if (!string.IsNullOrEmpty(unlockDoorStr) && FormKey.TryFactory(unlockDoorStr, out var unlockDoorFormKey))
                        {
                            builder.AsUnlockDoors(unlockDoorFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid door FormKey: {unlockDoorStr}");
                        }
                    }
                    else
                    {
                        // Unlock all owned doors
                        builder.AsUnlockDoors();
                    }
                    break;

                case "dismount":
                    builder.AsDismount();
                    break;

                case "acquire":
                    if (options?.ContainsKey("objectRef") == true)
                    {
                        var acquireObjStr = options["objectRef"]?.ToString();
                        if (!string.IsNullOrEmpty(acquireObjStr) && FormKey.TryFactory(acquireObjStr, out var acquireObjFormKey))
                        {
                            builder.AsAcquire(acquireObjFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid object FormKey: {acquireObjStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Acquire packages require a valid object reference (objectRef)");
                    }
                    break;

                case "escortto":
                    if (options?.ContainsKey("escortRef") == true && options?.ContainsKey("destinationRef") == true)
                    {
                        var escortRefStr = options["escortRef"]?.ToString();
                        var escortDestStr = options["destinationRef"]?.ToString();

                        if (!string.IsNullOrEmpty(escortRefStr) && FormKey.TryFactory(escortRefStr, out var escortFormKey) &&
                            !string.IsNullOrEmpty(escortDestStr) && FormKey.TryFactory(escortDestStr, out var escortDestFormKey))
                        {
                            builder.AsEscort(escortFormKey, escortDestFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail("Invalid FormKey for escort target or destination");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Escort packages require both escort reference (escortRef) and destination reference (destinationRef)");
                    }
                    break;

                case "say":
                    if (options?.ContainsKey("topicRef") == true)
                    {
                        var topicRefStr = options["topicRef"]?.ToString();
                        if (!string.IsNullOrEmpty(topicRefStr) && FormKey.TryFactory(topicRefStr, out var topicFormKey))
                        {
                            FormKey? sayLocation = null;
                            if (options?.ContainsKey("locationRef") == true)
                            {
                                var locStr = options["locationRef"]?.ToString();
                                if (!string.IsNullOrEmpty(locStr) && FormKey.TryFactory(locStr, out var locFormKey))
                                {
                                    sayLocation = locFormKey;
                                }
                            }
                            builder.AsSay(topicFormKey, sayLocation);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid topic FormKey: {topicRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Say packages require a valid topic reference (topicRef)");
                    }
                    break;

                case "shout":
                    if (options?.ContainsKey("shoutRef") == true)
                    {
                        var shoutRefStr = options["shoutRef"]?.ToString();
                        if (!string.IsNullOrEmpty(shoutRefStr) && FormKey.TryFactory(shoutRefStr, out var shoutFormKey))
                        {
                            FormKey? shoutTarget = null;
                            if (options?.ContainsKey("targetRef") == true)
                            {
                                var targetStr = options["targetRef"]?.ToString();
                                if (!string.IsNullOrEmpty(targetStr) && FormKey.TryFactory(targetStr, out var targetFormKey))
                                {
                                    shoutTarget = targetFormKey;
                                }
                            }
                            builder.AsShout(shoutFormKey, shoutTarget);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid shout FormKey: {shoutRefStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Shout packages require a valid shout reference (shoutRef)");
                    }
                    break;

                case "followto":
                    if (options?.ContainsKey("followRef") == true && options?.ContainsKey("destinationRef") == true)
                    {
                        var followRefStr = options["followRef"]?.ToString();
                        var followDestStr = options["destinationRef"]?.ToString();

                        if (!string.IsNullOrEmpty(followRefStr) && FormKey.TryFactory(followRefStr, out var followFormKey) &&
                            !string.IsNullOrEmpty(followDestStr) && FormKey.TryFactory(followDestStr, out var followDestFormKey))
                        {
                            builder.AsFollowTo(followFormKey, followDestFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail("Invalid FormKey for follow target or destination");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("FollowTo packages require both follow reference (followRef) and destination reference (destinationRef)");
                    }
                    break;

                case "holdposition":
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var holdPosStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(holdPosStr) && FormKey.TryFactory(holdPosStr, out var holdPosFormKey))
                        {
                            builder.AsHoldPosition(holdPosFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid position marker FormKey: {holdPosStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("HoldPosition packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "keepaneyeon":
                    if (options?.ContainsKey("targetRef") == true)
                    {
                        var watchTargetStr = options["targetRef"]?.ToString();
                        if (!string.IsNullOrEmpty(watchTargetStr) && FormKey.TryFactory(watchTargetStr, out var watchTargetFormKey))
                        {
                            builder.AsKeepAnEyeOn(watchTargetFormKey);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid watch target FormKey: {watchTargetStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("KeepAnEyeOn packages require a valid target reference (targetRef)");
                    }
                    break;

                case "hover":
                    var hoverRadius = Convert.ToUInt16(options?.GetValueOrDefault("radius", 1000) ?? 1000);
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var hoverMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(hoverMarkerStr) && FormKey.TryFactory(hoverMarkerStr, out var hoverMarkerFormKey))
                        {
                            builder.AsHover(hoverMarkerFormKey, hoverRadius);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid hover marker FormKey: {hoverMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Hover packages require a valid marker reference (markerRef)");
                    }
                    break;

                case "orbit":
                    var orbitRadius = Convert.ToUInt16(options?.GetValueOrDefault("radius", 500) ?? 500);
                    if (options?.ContainsKey("markerRef") == true)
                    {
                        var orbitMarkerStr = options["markerRef"]?.ToString();
                        if (!string.IsNullOrEmpty(orbitMarkerStr) && FormKey.TryFactory(orbitMarkerStr, out var orbitMarkerFormKey))
                        {
                            builder.AsOrbit(orbitMarkerFormKey, orbitRadius);
                        }
                        else
                        {
                            return Result<string>.Fail($"Invalid orbit marker FormKey: {orbitMarkerStr}");
                        }
                    }
                    else
                    {
                        return Result<string>.Fail("Orbit packages require a valid marker reference (markerRef)");
                    }
                    break;

                default:
                    return Result<string>.Fail(
                        $"Unknown package type: {packageType}",
                        suggestions: new List<string>
                        {
                            "Valid types: sandbox, travel, sleep, eat, follow, guard, patrol, useitemat, activate, sit, useidlemarker, idle, flee, accompany, escort, castmagic, cast, dialogue, find, search, ambush, wander, wait, relax, forcegreet, greet, useweapon, usemagic, lockdoors, unlockdoors, dismount, acquire, escortto, say, shout, followto, holdposition, keepaneyeon, hover, orbit"
                        });
            }

            // Apply common options
            if (options?.ContainsKey("location") == true)
            {
                var locationStr = options["location"].ToString();
                if (!string.IsNullOrEmpty(locationStr) && FormKey.TryFactory(locationStr, out var locationFormKey))
                {
                    builder.WithLocation(locationFormKey);
                }
            }

            var package = builder.Build();
            mod.WriteToBinary(pluginPath);

            _logger.Info($"Added package: {editorId} (FormKey: {package.FormKey})");
            return Result<string>.Ok(package.FormKey.ToString());
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to add package: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Attach an existing package to an NPC.
    /// </summary>
    public Result<string> AttachPackageToNpc(
        string pluginPath,
        string npcEditorId,
        string packageEditorId)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result<string>.Fail($"Plugin not found: {pluginPath}");
            }

            var mod = SkyrimMod.CreateFromBinary(
                pluginPath,
                SkyrimRelease.SkyrimSE,
                new Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters());

            // Ensure proper FormID allocation
            if (mod.ModHeader.Stats.NextFormID < 0x800)
            {
                mod.ModHeader.Stats.NextFormID = 0x800;
            }

            // Find NPC
            var npc = mod.Npcs.FirstOrDefault(n => n.EditorID == npcEditorId);
            if (npc == null)
            {
                return Result<string>.Fail(
                    $"NPC not found: {npcEditorId}",
                    suggestions: new List<string>
                    {
                        "Use 'esp info' to list NPCs in the plugin",
                        "Check the NPC editor ID spelling"
                    });
            }

            // Find Package
            var package = mod.Packages.FirstOrDefault(p => p.EditorID == packageEditorId);
            if (package == null)
            {
                return Result<string>.Fail(
                    $"Package not found: {packageEditorId}",
                    suggestions: new List<string>
                    {
                        "Create the package first with 'esp add-package'",
                        "Check the package editor ID spelling"
                    });
            }

            // Attach package to NPC
            npc.Packages.Add(package.ToLink());

            mod.WriteToBinary(pluginPath);

            _logger.Info($"Attached package '{packageEditorId}' to NPC '{npcEditorId}'");
            return Result<string>.Ok($"Package attached successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to attach package: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Find a record by FormKey string
    /// </summary>
    private Result<IMajorRecordGetter> FindRecordByFormKey(ISkyrimModGetter mod, string formKeyStr)
    {
        try
        {
            if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(formKeyStr, out var formKey))
            {
                return Result<IMajorRecordGetter>.Fail(
                    $"Invalid FormKey format: {formKeyStr}",
                    suggestions: new List<string>
                    {
                        "Use format: 0x000800 or PluginName.esp:0x000800",
                        "Use --editor-id if you know the EditorID instead"
                    });
            }

            foreach (var record in mod.EnumerateMajorRecords())
            {
                if (record.FormKey == formKey)
                {
                    return Result<IMajorRecordGetter>.Ok(record);
                }
            }

            return Result<IMajorRecordGetter>.Fail(
                $"Record with FormKey {formKey} not found in plugin",
                suggestions: new List<string>
                {
                    "Verify the FormKey exists in this plugin",
                    "Use 'esp info' to see record counts"
                });
        }
        catch (Exception ex)
        {
            return Result<IMajorRecordGetter>.Fail($"Error finding record: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Find a record by EditorID and type
    /// </summary>
    private Result<IMajorRecordGetter> FindRecordByEditorId(ISkyrimModGetter mod, string editorId, string recordType)
    {
        try
        {
            IMajorRecordGetter? found = null;

            switch (recordType.ToLowerInvariant())
            {
                case "spell":
                    found = mod.Spells.FirstOrDefault(s => s.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "weapon":
                    found = mod.Weapons.FirstOrDefault(w => w.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "armor":
                    found = mod.Armors.FirstOrDefault(a => a.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "quest":
                    found = mod.Quests.FirstOrDefault(q => q.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "npc":
                    found = mod.Npcs.FirstOrDefault(n => n.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "perk":
                    found = mod.Perks.FirstOrDefault(p => p.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "faction":
                    found = mod.Factions.FirstOrDefault(f => f.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "book":
                    found = mod.Books.FirstOrDefault(b => b.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "miscitem":
                    found = mod.MiscItems.FirstOrDefault(m => m.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "global":
                    found = mod.Globals.FirstOrDefault(g => g.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "leveleditem":
                    found = mod.LeveledItems.FirstOrDefault(l => l.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "formlist":
                    found = mod.FormLists.FirstOrDefault(f => f.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "outfit":
                    found = mod.Outfits.FirstOrDefault(o => o.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "location":
                    found = mod.Locations.FirstOrDefault(l => l.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "encounterzone":
                    found = mod.EncounterZones.FirstOrDefault(e => e.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                case "constructibleobject":
                case "cobj":
                case "recipe":
                    found = mod.ConstructibleObjects.FirstOrDefault(c => c.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
                    break;
                default:
                    return Result<IMajorRecordGetter>.Fail(
                        $"Unsupported record type: {recordType}",
                        suggestions: new List<string>
                        {
                            "Supported types: spell, weapon, armor, quest, npc, perk, faction, book, miscitem, global, leveleditem, formlist, outfit, location, encounterzone, constructibleobject",
                            "Use --form-id instead for other record types"
                        });
            }

            if (found == null)
            {
                return Result<IMajorRecordGetter>.Fail(
                    $"{recordType} with EditorID '{editorId}' not found",
                    suggestions: new List<string>
                    {
                        "Check EditorID spelling",
                        "Use 'esp info' to see record counts",
                        "Try --form-id if you know the FormKey"
                    });
            }

            return Result<IMajorRecordGetter>.Ok(found);
        }
        catch (Exception ex)
        {
            return Result<IMajorRecordGetter>.Fail($"Error finding record: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Extract properties from a record based on its type
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractRecordProperties(IMajorRecordGetter record, bool includeRaw)
    {
        try
        {
            return record switch
            {
                ISpellGetter spell => ExtractSpellProperties(spell),
                IWeaponGetter weapon => ExtractWeaponProperties(weapon),
                IArmorGetter armor => ExtractArmorProperties(armor),
                IQuestGetter quest => ExtractQuestProperties(quest),
                INpcGetter npc => ExtractNpcProperties(npc),
                IPerkGetter perk => ExtractPerkProperties(perk),
                IPlacedObjectGetter placed => ExtractPlacedObjectProperties(placed),
                IConstructibleObjectGetter cobj => ExtractConstructibleObjectProperties(cobj),
                _ => includeRaw ? ExtractPropertiesViaReflection(record) : Result<Dictionary<string, object?>>.Ok(new Dictionary<string, object?>())
            };
        }
        catch (Exception ex)
        {
            return Result<Dictionary<string, object?>>.Fail($"Failed to extract properties: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Extract spell-specific properties
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractSpellProperties(ISpellGetter spell)
    {
        var props = new Dictionary<string, object?>
        {
            ["Name"] = spell.Name?.String,
            ["Type"] = spell.Type.ToString(),
            ["BaseCost"] = spell.BaseCost,
            ["CastType"] = spell.CastType.ToString(),
            ["TargetType"] = spell.TargetType.ToString(),
            ["CastDuration"] = spell.CastDuration,
            ["Range"] = spell.Range,
            ["EffectCount"] = spell.Effects.Count
        };

        if (spell.EquipmentType != null && !spell.EquipmentType.IsNull)
        {
            props["EquipmentType"] = spell.EquipmentType.FormKey.ToString();
        }

        var effects = new List<Dictionary<string, object?>>();
        foreach (var effect in spell.Effects)
        {
            var effectProps = new Dictionary<string, object?>
            {
                ["BaseEffect"] = effect.BaseEffect.FormKey.ToString(),
                ["Magnitude"] = effect.Data?.Magnitude ?? 0,
                ["Duration"] = effect.Data?.Duration ?? 0,
                ["Area"] = effect.Data?.Area ?? 0
            };
            effects.Add(effectProps);
        }
        props["Effects"] = effects;

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract weapon-specific properties
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractWeaponProperties(IWeaponGetter weapon)
    {
        var props = new Dictionary<string, object?>
        {
            ["Name"] = weapon.Name?.String,
            ["Damage"] = weapon.BasicStats?.Damage ?? 0,
            ["Weight"] = weapon.BasicStats?.Weight ?? 0,
            ["Value"] = weapon.BasicStats?.Value ?? 0,
            ["CriticalDamage"] = weapon.Critical?.Damage ?? 0,
            ["Speed"] = weapon.Data?.Speed ?? 0,
            ["Reach"] = weapon.Data?.Reach ?? 0,
            ["AnimationType"] = weapon.Data?.AnimationType.ToString()
        };

        if (weapon.Keywords != null)
        {
            props["Keywords"] = weapon.Keywords.Select(k => k.FormKey.ToString()).ToList();
        }

        if (weapon.Template != null && !weapon.Template.IsNull)
        {
            props["Template"] = weapon.Template.FormKey.ToString();
        }

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract armor-specific properties
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractArmorProperties(IArmorGetter armor)
    {
        var props = new Dictionary<string, object?>
        {
            ["Name"] = armor.Name?.String,
            ["ArmorRating"] = armor.ArmorRating,
            ["Weight"] = armor.Weight,
            ["Value"] = armor.Value,
            ["BodyTemplate"] = armor.BodyTemplate?.ToString()
        };

        if (armor.Keywords != null)
        {
            props["Keywords"] = armor.Keywords.Select(k => k.FormKey.ToString()).ToList();
        }

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract crafting-recipe (ConstructibleObject) properties: what it makes, at which
    /// workbench, and what ingredients it consumes.
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractConstructibleObjectProperties(IConstructibleObjectGetter cobj)
    {
        var props = new Dictionary<string, object?>
        {
            ["CreatedObject"] = cobj.CreatedObject.IsNull ? null : cobj.CreatedObject.FormKey.ToString(),
            ["CreatedObjectCount"] = cobj.CreatedObjectCount,
            ["WorkbenchKeyword"] = (cobj.WorkbenchKeyword == null || cobj.WorkbenchKeyword.IsNull) ? null : cobj.WorkbenchKeyword.FormKey.ToString()
        };

        var items = new List<Dictionary<string, object?>>();
        if (cobj.Items != null)
        {
            foreach (var entry in cobj.Items)
            {
                items.Add(new Dictionary<string, object?>
                {
                    ["Item"] = entry.Item.Item.FormKey.ToString(),
                    ["Count"] = entry.Item.Count
                });
            }
        }
        props["Items"] = items;

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract quest-specific properties
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractQuestProperties(IQuestGetter quest)
    {
        var props = new Dictionary<string, object?>
        {
            ["Name"] = quest.Name?.String,
            ["Priority"] = quest.Priority,
            ["Flags"] = quest.Flags.ToString(),
            ["StageCount"] = quest.Stages.Count,
            ["AliasCount"] = quest.Aliases.Count
        };

        // TODO: Add Event property once API is confirmed
        // if (quest.Event != null && !quest.Event.IsNull)
        // {
        //     props["Event"] = quest.Event.FormKey.ToString();
        // }

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract NPC-specific properties
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractNpcProperties(INpcGetter npc)
    {
        var props = new Dictionary<string, object?>
        {
            ["Name"] = npc.Name?.String,
            ["Race"] = npc.Race.FormKey.ToString(),
            ["Level"] = npc.Configuration?.Level?.ToString(),
            ["Health"] = npc.Configuration?.HealthOffset ?? 0,
            ["Magicka"] = npc.Configuration?.MagickaOffset ?? 0,
            ["Stamina"] = npc.Configuration?.StaminaOffset ?? 0
        };

        if (npc.Class != null && !npc.Class.IsNull)
        {
            props["Class"] = npc.Class.FormKey.ToString();
        }

        if (npc.Keywords != null)
        {
            props["Keywords"] = npc.Keywords.Select(k => k.FormKey.ToString()).ToList();
        }

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract perk-specific properties
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractPerkProperties(IPerkGetter perk)
    {
        var props = new Dictionary<string, object?>
        {
            ["Name"] = perk.Name?.String,
            ["Description"] = perk.Description?.String,
            ["EffectCount"] = perk.Effects.Count
        };

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract properties for a placed object reference (base object + transform).
    /// Placement is a nested record, so plain reflection only reports the type name -
    /// this flattens position/rotation into readable values.
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractPlacedObjectProperties(IPlacedObjectGetter placed)
    {
        var props = new Dictionary<string, object?>
        {
            ["base"] = placed.Base.FormKey.ToString(),
            ["scale"] = placed.Scale ?? 1.0f
        };

        if (placed.Placement is { } placement)
        {
            props["posX"] = placement.Position.X;
            props["posY"] = placement.Position.Y;
            props["posZ"] = placement.Position.Z;
            props["rotX"] = placement.Rotation.X;
            props["rotY"] = placement.Rotation.Y;
            props["rotZ"] = placement.Rotation.Z;
        }

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract properties via reflection (fallback for unknown types)
    /// </summary>
    private Result<Dictionary<string, object?>> ExtractPropertiesViaReflection(IMajorRecordGetter record)
    {
        var props = new Dictionary<string, object?>();
        var type = record.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            try
            {
                if (prop.Name is "EditorID" or "FormKey")
                    continue;

                var value = prop.GetValue(record);
                if (value != null)
                {
                    props[prop.Name] = value.ToString();
                }
            }
            catch
            {
                // Skip properties that throw exceptions
            }
        }

        return Result<Dictionary<string, object?>>.Ok(props);
    }

    /// <summary>
    /// Extract conditions from a record
    /// Verified to work with: Perk, Package, IdleAnimation, MagicEffect
    /// </summary>
    private Result<List<ConditionInfo>> ExtractConditions(IMajorRecordGetter record)
    {
        var conditions = new List<ConditionInfo>();

        try
        {
            IReadOnlyList<IConditionGetter>? conditionList = null;

            switch (record)
            {
                case IPerkGetter perk:
                    conditionList = perk.Conditions;
                    break;
                case IPackageGetter package:
                    conditionList = package.Conditions;
                    break;
                case IIdleAnimationGetter idle:
                    conditionList = idle.Conditions;
                    break;
                // Note: MagicEffect conditions exist but are more complex
                // Spells, Weapons, Armor do NOT have direct Conditions properties
            }

            if (conditionList == null || conditionList.Count == 0)
            {
                return Result<List<ConditionInfo>>.Ok(conditions);
            }

            foreach (var condition in conditionList)
            {
                if (condition.Data == null)
                    continue;

                var functionName = condition.Data.GetType().Name.Replace("ConditionData", "");

                var condInfo = new ConditionInfo
                {
                    FunctionName = functionName,
                    Operator = condition.CompareOperator.ToString()
                };

                // Handle ConditionFloat (most common) - use getter interface for binary overlay compatibility
                if (condition is IConditionFloatGetter condFloat)
                {
                    condInfo.ComparisonValue = condFloat.ComparisonValue;
                }
                // Handle ConditionGlobal (uses global variable)
                else if (condition is IConditionGlobalGetter condGlobal)
                {
                    condInfo.ComparisonValue = 0; // Global comparison
                    if (condGlobal.ComparisonValue.FormKey != null)
                    {
                        condInfo.ParameterA = condGlobal.ComparisonValue.FormKey.ToString();
                    }
                }

                // Extract flags and run-on type (common to all conditions)
                condInfo.Flags = condition.Flags.ToString();

                // RunOnType varies by ConditionData subclass - try to get it via reflection
                var runOnProp = condition.Data.GetType().GetProperty("RunOnType");
                if (runOnProp != null)
                {
                    var runOnValue = runOnProp.GetValue(condition.Data);
                    condInfo.RunOn = runOnValue?.ToString() ?? "Unknown";
                }

                conditions.Add(condInfo);
            }

            return Result<List<ConditionInfo>>.Ok(conditions);
        }
        catch (Exception ex)
        {
            return Result<List<ConditionInfo>>.Fail($"Failed to extract conditions: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Create an override patch for a record
    /// </summary>
    public Result<string> CreateOverride(
        string sourcePluginPath,
        string outputPluginName,
        string? editorId,
        string? formId,
        string? recordType,
        bool removeConditions = false,
        string? dataFolder = null)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
            {
                return Result<string>.Fail(
                    $"Source plugin not found: {sourcePluginPath}",
                    suggestions: new List<string>
                    {
                        "Check the file path is correct",
                        "Use an absolute path if relative path fails"
                    });
            }

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePluginPath, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? sourceRecord = null;

            if (!string.IsNullOrEmpty(formId))
            {
                var findResult = FindRecordByFormKey(sourceMod, formId);
                if (!findResult.Success)
                {
                    return Result<string>.Fail(findResult.Error ?? "Record not found", findResult.ErrorContext);
                }
                sourceRecord = findResult.Value;
            }
            else if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(recordType))
            {
                var findResult = FindRecordByEditorId(sourceMod, editorId, recordType);
                if (!findResult.Success)
                {
                    return Result<string>.Fail(findResult.Error ?? "Record not found", findResult.ErrorContext);
                }
                sourceRecord = findResult.Value;
            }
            else
            {
                return Result<string>.Fail(
                    "Must provide either FormID or both EditorID and RecordType",
                    suggestions: new List<string>
                    {
                        "Use --form-id for FormKey-based lookup",
                        "Use --editor-id and --type for EditorID-based lookup"
                    });
            }

            if (sourceRecord == null)
            {
                return Result<string>.Fail(
                    $"Record not found: {editorId ?? formId}",
                    suggestions: new List<string>
                    {
                        "Use 'esp info' to see available records",
                        "Check spelling of EditorID",
                        "Verify FormID is correct"
                    });
            }

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            patchMod.ModHeader.MasterReferences.Add(new MasterReference
            {
                Master = sourceMod.ModKey
            });

            var overrideResult = CreateOverrideRecord(patchMod, sourceRecord, removeConditions);
            if (!overrideResult.Success)
            {
                return Result<string>.Fail(overrideResult.Error ?? "Failed to create override", overrideResult.ErrorContext);
            }

            var outputDir = !string.IsNullOrEmpty(dataFolder)
                ? dataFolder
                : Path.GetDirectoryName(sourcePluginPath) ?? Directory.GetCurrentDirectory();

            var outputPath = Path.Combine(outputDir, outputPluginName);

            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Created override patch: {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Failed to create override: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Create an ESL override patch that updates selected fields on an existing spell.
    /// The source plugin is loaded read-only and is never modified.
    /// </summary>
    public Result<string> UpdateSpell(
        string sourcePluginPath,
        string outputPluginPath,
        string editorId,
        SpellType? spellType = null,
        string? equipmentTypeFormKey = null)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
                return Result<string>.Fail($"Source plugin not found: {sourcePluginPath}");

            if (spellType == null && string.IsNullOrWhiteSpace(equipmentTypeFormKey))
                return Result<string>.Fail("Specify at least one spell field to update: type or equipment type.");

            var sourcePath = Path.GetFullPath(sourcePluginPath);
            var outputPath = Path.GetFullPath(outputPluginPath);
            if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
                return Result<string>.Fail("Output path must differ from the source plugin path.");

            if (!outputPath.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPath.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
                return Result<string>.Fail("Output path must end in .esp or .esl.");

            if (File.Exists(outputPath))
                return Result<string>.Fail($"Output file already exists: {outputPath}",
                    suggestions: new List<string> { "Choose a new output path; existing files are never overwritten." });

            if (!string.IsNullOrWhiteSpace(equipmentTypeFormKey) &&
                !FormKey.TryFactory(equipmentTypeFormKey, out _))
                return Result<string>.Fail(
                    $"Invalid equipment type FormKey: '{equipmentTypeFormKey}'. Expected 'FORMID:MODNAME', for example '013F44:Skyrim.esm'.");

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePath, SkyrimRelease.SkyrimSE);
            var sourceSpell = sourceMod.Spells.FirstOrDefault(spell =>
                string.Equals(spell.EditorID, editorId, StringComparison.OrdinalIgnoreCase));
            if (sourceSpell == null)
                return Result<string>.Fail($"Spell not found in source plugin: {editorId}",
                    suggestions: new List<string> { "Use 'esp list-records <source> --type spell' to find its EditorID." });

            var patchName = Path.GetFileName(outputPath);
            var patchMod = new SkyrimMod(ModKey.FromFileName(patchName), SkyrimRelease.SkyrimSE);
            patchMod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;

            // Preserve the source's master chain, then add the source itself.
            foreach (var master in sourceMod.ModHeader.MasterReferences)
            {
                if (!patchMod.ModHeader.MasterReferences.Any(existing => existing.Master == master.Master))
                    patchMod.ModHeader.MasterReferences.Add(new MasterReference { Master = master.Master });
            }
            if (!patchMod.ModHeader.MasterReferences.Any(master => master.Master == sourceMod.ModKey))
                patchMod.ModHeader.MasterReferences.Add(new MasterReference { Master = sourceMod.ModKey });

            var spellOverride = (Spell)sourceSpell.DeepCopy();
            if (spellType.HasValue)
                spellOverride.Type = spellType.Value;
            if (!string.IsNullOrWhiteSpace(equipmentTypeFormKey))
            {
                FormKey.TryFactory(equipmentTypeFormKey, out var equipmentTypeKey);
                spellOverride.EquipmentType.SetTo(equipmentTypeKey);
            }
            patchMod.Spells.Add(spellOverride);

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Created ESL spell override patch: {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to update spell: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Add starting stock to an EXISTING container record (by EditorID) already in this plugin.
    /// Simple direct edit, no override machinery - same shape as AddPlacedObjectToLocalCell.
    /// </summary>
    /// <summary>
    /// Removes every item entry from a container in THIS SAME plugin, so it can be re-seeded
    /// cleanly via AddItemToLocalContainer. Needed because there is no per-entry remove - a
    /// container's starting stock is only ever additive otherwise, which is a problem the moment
    /// a previous add was wrong (bad FormKey, duplicate) or the container already had stock from
    /// an earlier build and a fresh add would double it rather than replace it.
    /// </summary>
    public Result<string> ClearLocalContainerItems(SkyrimMod mod, string containerEditorId)
    {
        try
        {
            var container = mod.Containers.FirstOrDefault(c => c.EditorID == containerEditorId);
            if (container == null)
            {
                return Result<string>.Fail($"Container not found in this plugin: {containerEditorId}");
            }

            var removedCount = container.Items?.Count ?? 0;
            container.Items = new Noggog.ExtendedList<ContainerEntry>();

            return Result<string>.Ok($"{containerEditorId} ({removedCount} entries removed)");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to clear local container items: {ex.Message}", ex.StackTrace);
        }
    }

    public Result<string> AddItemToLocalContainer(SkyrimMod mod, string containerEditorId, FormKey itemKey, int count)
    {
        try
        {
            var container = mod.Containers.FirstOrDefault(c => c.EditorID == containerEditorId);
            if (container == null)
            {
                return Result<string>.Fail($"Container not found in this plugin: {containerEditorId}");
            }

            container.Items ??= new Noggog.ExtendedList<ContainerEntry>();
            container.Items.Add(new ContainerEntry
            {
                Item = new ContainerItem
                {
                    Item = itemKey.ToLink<IItemGetter>(),
                    Count = count,
                },
            });

            if (itemKey.ModKey != mod.ModKey &&
                !mod.ModHeader.MasterReferences.Any(m => m.Master == itemKey.ModKey))
            {
                mod.ModHeader.MasterReferences.Add(new MasterReference { Master = itemKey.ModKey });
            }

            return Result<string>.Ok(containerEditorId);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to add item to local container: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Add a PlacedObject reference into a cell that already exists IN THE SAME in-memory mod
    /// being edited (e.g. a cell this plugin itself created earlier in the same session). Unlike
    /// AddPlacedObject below, this needs no plugins.txt/load-order/override-patch machinery at
    /// all - it's a plain same-file load-modify-save, the same shape as every other single-file
    /// command in this class. Also avoids a real bug in AddPlacedObject's approach: building a
    /// load-order there requires listing the OUTPUT plugin itself in plugins.txt (since the
    /// target cell lives in it), which opens that exact output path via
    /// CreateFromBinaryOverlay - a lazy/memory-mapped reader that keeps the file open - and then
    /// immediately fails writing back to that same still-open path. This method sidesteps the
    /// whole class of bug by never needing an overlay-based load order in the first place.
    /// </summary>
    public Result<string> AddPlacedObjectToLocalCell(
        SkyrimMod mod,
        string cellEditorId,
        FormKey baseObjectKey,
        float posX, float posY, float posZ,
        float rotX, float rotY, float rotZ,
        float scale = 1.0f)
    {
        try
        {
            Cell? targetCell = null;
            foreach (var block in mod.Cells)
            {
                foreach (var subBlock in block.SubBlocks)
                {
                    targetCell = subBlock.Cells.FirstOrDefault(c => c.EditorID == cellEditorId);
                    if (targetCell != null) break;
                }
                if (targetCell != null) break;
            }

            if (targetCell == null)
            {
                return Result<string>.Fail(
                    $"Cell not found in this plugin: {cellEditorId}",
                    suggestions: new List<string> { "Verify the cell editor ID and that it was created in this same plugin (use 'esp create-cell')" });
            }

            var newRef = new PlacedObject(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE);
            newRef.Base.SetTo(baseObjectKey);
            newRef.Placement = new Placement
            {
                Position = new P3Float(posX, posY, posZ),
                Rotation = new P3Float(rotX, rotY, rotZ)
            };
            newRef.Scale = scale;

            targetCell.Temporary.Add(newRef);

            if (baseObjectKey.ModKey != mod.ModKey &&
                !mod.ModHeader.MasterReferences.Any(m => m.Master == baseObjectKey.ModKey))
            {
                mod.ModHeader.MasterReferences.Add(new MasterReference { Master = baseObjectKey.ModKey });
            }

            return Result<string>.Ok(newRef.FormKey.ToString());
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to add placed object to local cell: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Add a brand new PlacedObject reference into an existing cell (owned by another
    /// master plugin, e.g. Skyrim.esm), without duplicating that cell's existing
    /// Temporary/Persistent reference lists. Uses a minimal cell override via
    /// ResolveContext/GetOrAddAsOverride so only the new reference is added - every
    /// other object already in the cell is left completely untouched.
    /// </summary>
    public Result<string> AddPlacedObject(
        string dataFolder,
        string cellFormKey,
        string baseObjectFormKey,
        string outputPluginName,
        float posX, float posY, float posZ,
        float rotX, float rotY, float rotZ,
        float scale = 1.0f,
        bool light = false)
    {
        try
        {
            if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(cellFormKey, out var cellKey))
            {
                return Result<string>.Fail(
                    $"Invalid cell FormKey format: {cellFormKey}",
                    suggestions: new List<string> { "Use format: PluginName.esp:0x000800" });
            }

            if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(baseObjectFormKey, out var baseKey))
            {
                return Result<string>.Fail(
                    $"Invalid base object FormKey format: {baseObjectFormKey}",
                    suggestions: new List<string> { "Use format: PluginName.esp:0x000800" });
            }

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var existingOutputPath = Path.Combine(dataFolder, outputPluginName);

            // Append to the patch if it already exists, so multiple placements can be
            // added across separate invocations instead of overwriting each other.
            var patchMod = File.Exists(existingOutputPath)
                ? SkyrimMod.CreateFromBinary(existingOutputPath, SkyrimRelease.SkyrimSE)
                : new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            // Build a typed link cache in real load order. Order matters: the cell context
            // must resolve to the WINNING override (e.g. JK's Jorrvaskr's version of the
            // cell), not Skyrim.esm's, otherwise the override we emit would carry vanilla
            // cell data and clobber whatever mod currently owns the cell record.
            var loadOrderListings = new List<IModListingGetter<ISkyrimModGetter>>();
            var pluginsTxt = Path.Combine(dataFolder, "plugins.txt");

            if (File.Exists(pluginsTxt))
            {
                foreach (var rawLine in File.ReadAllLines(pluginsTxt))
                {
                    var line = rawLine.Trim().TrimStart('*').Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    var pluginPath = Path.Combine(dataFolder, line);
                    if (!File.Exists(pluginPath))
                    {
                        _logger.Warning($"Plugin listed in plugins.txt not found, skipping: {line}");
                        continue;
                    }

                    var listingMod = SkyrimMod.CreateFromBinaryOverlay(pluginPath, SkyrimRelease.SkyrimSE);
                    loadOrderListings.Add(new ModListing<ISkyrimModGetter>(listingMod, true));
                }
            }

            if (loadOrderListings.Count == 0)
            {
                return Result<string>.Fail(
                    "No load order could be built",
                    $"Expected a plugins.txt listing the relevant plugins in load order inside: {dataFolder}",
                    new List<string>
                    {
                        "Create a plugins.txt in the data folder listing masters and mods in load order",
                        "The cell's owning plugin must be included so the winning override resolves correctly"
                    });
            }

            var linkCache = loadOrderListings.ToImmutableLinkCache();

            if (!linkCache.TryResolveContext<ICell, ICellGetter>(cellKey, out var cellContext))
            {
                return Result<string>.Fail(
                    $"Cell not found in the provided load order: {cellFormKey}",
                    suggestions: new List<string>
                    {
                        "Verify the cell FormKey is correct",
                        "Ensure the plugin owning the cell is listed in plugins.txt"
                    });
            }
            var cellOverride = cellContext.GetOrAddAsOverride(patchMod);

            var newRef = new PlacedObject(patchMod.GetNextFormKey(), SkyrimRelease.SkyrimSE);
            newRef.Base.SetTo(baseKey);
            newRef.Placement = new Placement
            {
                Position = new P3Float(posX, posY, posZ),
                Rotation = new P3Float(rotX, rotY, rotZ)
            };
            newRef.Scale = scale;

            cellOverride.Temporary.Add(newRef);

            if (baseKey.ModKey != patchMod.ModKey &&
                !patchMod.ModHeader.MasterReferences.Any(m => m.Master == baseKey.ModKey))
            {
                patchMod.ModHeader.MasterReferences.Add(new MasterReference { Master = baseKey.ModKey });
            }

            if (light)
            {
                patchMod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
            }

            var outputPath = Path.Combine(dataFolder, outputPluginName);
            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Added placed object {newRef.FormKey} to {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to add placed object: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Build a typed link cache from a plugins.txt-ordered folder, so records resolve
    /// to their WINNING override rather than whichever plugin happens to be read first.
    /// </summary>
    private Result<ILinkCache<ISkyrimMod, ISkyrimModGetter>> BuildOrderedLinkCache(
        string dataFolder,
        string? excludePluginName = null)
    {
        var listings = new List<IModListingGetter<ISkyrimModGetter>>();
        var pluginsTxt = Path.Combine(dataFolder, "plugins.txt");

        if (!File.Exists(pluginsTxt))
        {
            return Result<ILinkCache<ISkyrimMod, ISkyrimModGetter>>.Fail(
                "plugins.txt not found",
                $"Expected a load order listing at: {pluginsTxt}");
        }

        foreach (var rawLine in File.ReadAllLines(pluginsTxt))
        {
            var line = rawLine.Trim().TrimStart('*').Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            // Never memory-map the plugin we are about to write to - the binary overlay
            // holds an open handle and the subsequent write would fail with a file lock.
            if (!string.IsNullOrEmpty(excludePluginName) &&
                string.Equals(line, excludePluginName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var pluginPath = Path.Combine(dataFolder, line);
            if (!File.Exists(pluginPath))
            {
                _logger.Warning($"Plugin listed in plugins.txt not found, skipping: {line}");
                continue;
            }

            var listingMod = SkyrimMod.CreateFromBinaryOverlay(pluginPath, SkyrimRelease.SkyrimSE);
            listings.Add(new ModListing<ISkyrimModGetter>(listingMod, true));
        }

        if (listings.Count == 0)
        {
            return Result<ILinkCache<ISkyrimMod, ISkyrimModGetter>>.Fail("No plugins could be loaded from plugins.txt");
        }

        return Result<ILinkCache<ISkyrimMod, ISkyrimModGetter>>.Ok(listings.ToImmutableLinkCache());
    }

    /// <summary>
    /// Copy an existing placed object reference into a patch as an override, optionally
    /// changing its base object, scale, position and rotation. The winning version of the
    /// reference (per the folder's plugins.txt load order) is used as the starting point,
    /// so this can be used to "bake in" another mod's edits before removing that mod.
    /// </summary>
    public Result<string> OverridePlacedObject(
        string dataFolder,
        string refFormKey,
        string outputPluginName,
        string? newBaseFormKey = null,
        float? scale = null,
        (float X, float Y, float Z)? position = null,
        (float X, float Y, float Z)? rotation = null,
        bool light = false)
    {
        try
        {
            if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(refFormKey, out var refKey))
            {
                return Result<string>.Fail(
                    $"Invalid reference FormKey format: {refFormKey}",
                    suggestions: new List<string> { "Use format: 078039:Skyrim.esm" });
            }

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var outputPath = Path.Combine(dataFolder, outputPluginName);

            var patchMod = File.Exists(outputPath)
                ? SkyrimMod.CreateFromBinary(outputPath, SkyrimRelease.SkyrimSE)
                : new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            var cacheResult = BuildOrderedLinkCache(dataFolder, outputPluginName);
            if (!cacheResult.Success)
            {
                return Result<string>.Fail(cacheResult.Error ?? "Failed to build link cache", cacheResult.ErrorContext);
            }

            if (!cacheResult.Value!.TryResolveContext<IPlacedObject, IPlacedObjectGetter>(refKey, out var refContext))
            {
                return Result<string>.Fail(
                    $"Placed object not found in load order: {refFormKey}",
                    suggestions: new List<string>
                    {
                        "Verify the reference FormKey",
                        "Ensure the owning plugin is listed in plugins.txt"
                    });
            }

            // Pulls in the parent cell override automatically.
            var refOverride = refContext.GetOrAddAsOverride(patchMod);

            if (!string.IsNullOrWhiteSpace(newBaseFormKey))
            {
                if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(newBaseFormKey, out var baseKey))
                {
                    return Result<string>.Fail($"Invalid base FormKey format: {newBaseFormKey}");
                }
                refOverride.Base.SetTo(baseKey);
            }

            if (scale.HasValue)
            {
                refOverride.Scale = scale.Value;
            }

            if (position.HasValue || rotation.HasValue)
            {
                refOverride.Placement ??= new Placement();
                if (position.HasValue)
                {
                    refOverride.Placement.Position = new P3Float(position.Value.X, position.Value.Y, position.Value.Z);
                }
                if (rotation.HasValue)
                {
                    refOverride.Placement.Rotation = new P3Float(rotation.Value.X, rotation.Value.Y, rotation.Value.Z);
                }
            }

            if (light)
            {
                patchMod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
            }

            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Overrode placed object {refKey} in {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to override placed object: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Remove a placed object reference from a plugin by FormKey.
    /// </summary>
    public Result<string> RemovePlacedObject(string pluginPath, string formKeyStr)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result<string>.Fail($"Plugin not found: {pluginPath}");
            }

            if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(formKeyStr, out var formKey))
            {
                return Result<string>.Fail(
                    $"Invalid FormKey format: {formKeyStr}",
                    suggestions: new List<string> { "Use format: 000800:MyPatch.esp" });
            }

            var mod = SkyrimMod.CreateFromBinary(pluginPath, SkyrimRelease.SkyrimSE);

            bool removed = false;
            foreach (var block in mod.Cells)
            {
                foreach (var subBlock in block.SubBlocks)
                {
                    foreach (var cell in subBlock.Cells)
                    {
                        if (cell.Temporary.Any(r => r.FormKey == formKey))
                        {
                            cell.Temporary.RemoveWhere(r => r.FormKey == formKey);
                            removed = true;
                        }
                        if (cell.Persistent.Any(r => r.FormKey == formKey))
                        {
                            cell.Persistent.RemoveWhere(r => r.FormKey == formKey);
                            removed = true;
                        }
                    }
                }
            }

            if (!removed)
            {
                return Result<string>.Fail(
                    $"Placed object {formKeyStr} not found in {Path.GetFileName(pluginPath)}",
                    suggestions: new List<string> { "Use 'esp list-records' to see what the plugin contains" });
            }

            mod.WriteToBinary(pluginPath);

            _logger.Info($"Removed placed object {formKeyStr} from {pluginPath}");
            return Result<string>.Ok(pluginPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to remove placed object: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Create an override record with optional modifications
    /// Overrides keep the same FormKey as the source record
    /// </summary>
    private Result<IMajorRecord> CreateOverrideRecord(
        SkyrimMod targetMod,
        IMajorRecordGetter sourceRecord,
        bool removeConditions)
    {
        try
        {
            switch (sourceRecord)
            {
                case ISpellGetter spell:
                    var spellOverride = (Spell)spell.DeepCopy();
                    // TODO: Add condition removal once Mutagen API is confirmed
                    // if (removeConditions && spellOverride.Conditions != null)
                    // {
                    //     spellOverride.Conditions.Clear();
                    // }
                    targetMod.Spells.Add(spellOverride);
                    return Result<IMajorRecord>.Ok(spellOverride);

                case IWeaponGetter weapon:
                    var weaponOverride = (Weapon)weapon.DeepCopy();
                    // TODO: Add condition removal once Mutagen API is confirmed
                    // if (removeConditions && weaponOverride.Conditions != null)
                    // {
                    //     weaponOverride.Conditions.Clear();
                    // }
                    targetMod.Weapons.Add(weaponOverride);
                    return Result<IMajorRecord>.Ok(weaponOverride);

                case IArmorGetter armor:
                    var armorOverride = (Armor)armor.DeepCopy();
                    // TODO: Add condition removal once Mutagen API is confirmed
                    // if (removeConditions && armorOverride.Conditions != null)
                    // {
                    //     armorOverride.Conditions.Clear();
                    // }
                    targetMod.Armors.Add(armorOverride);
                    return Result<IMajorRecord>.Ok(armorOverride);

                case IQuestGetter quest:
                    var questOverride = (Quest)quest.DeepCopy();
                    targetMod.Quests.Add(questOverride);
                    return Result<IMajorRecord>.Ok(questOverride);

                case INpcGetter npc:
                    var npcOverride = (Npc)npc.DeepCopy();
                    targetMod.Npcs.Add(npcOverride);
                    return Result<IMajorRecord>.Ok(npcOverride);

                case IPerkGetter perk:
                    var perkOverride = (Perk)perk.DeepCopy();
                    if (removeConditions && perkOverride.Conditions != null)
                    {
                        perkOverride.Conditions.Clear();
                    }
                    targetMod.Perks.Add(perkOverride);
                    return Result<IMajorRecord>.Ok(perkOverride);

                case IFactionGetter faction:
                    var factionOverride = (Faction)faction.DeepCopy();
                    targetMod.Factions.Add(factionOverride);
                    return Result<IMajorRecord>.Ok(factionOverride);

                case IBookGetter book:
                    var bookOverride = (Book)book.DeepCopy();
                    targetMod.Books.Add(bookOverride);
                    return Result<IMajorRecord>.Ok(bookOverride);

                case IMiscItemGetter miscItem:
                    var miscOverride = (MiscItem)miscItem.DeepCopy();
                    targetMod.MiscItems.Add(miscOverride);
                    return Result<IMajorRecord>.Ok(miscOverride);

                case IGlobalGetter global:
                    var globalOverride = (Global)global.DeepCopy();
                    targetMod.Globals.Add(globalOverride);
                    return Result<IMajorRecord>.Ok(globalOverride);

                case ILeveledItemGetter leveledItem:
                    var leveledItemOverride = (LeveledItem)leveledItem.DeepCopy();
                    targetMod.LeveledItems.Add(leveledItemOverride);
                    return Result<IMajorRecord>.Ok(leveledItemOverride);

                case IFormListGetter formList:
                    var formListOverride = (FormList)formList.DeepCopy();
                    targetMod.FormLists.Add(formListOverride);
                    return Result<IMajorRecord>.Ok(formListOverride);

                case IOutfitGetter outfit:
                    var outfitOverride = (Outfit)outfit.DeepCopy();
                    targetMod.Outfits.Add(outfitOverride);
                    return Result<IMajorRecord>.Ok(outfitOverride);

                case ILocationGetter location:
                    var locationOverride = (Location)location.DeepCopy();
                    targetMod.Locations.Add(locationOverride);
                    return Result<IMajorRecord>.Ok(locationOverride);

                case IEncounterZoneGetter encounterZone:
                    var encounterZoneOverride = (EncounterZone)encounterZone.DeepCopy();
                    targetMod.EncounterZones.Add(encounterZoneOverride);
                    return Result<IMajorRecord>.Ok(encounterZoneOverride);

                default:
                    return Result<IMajorRecord>.Fail(
                        $"Unsupported record type for override: {sourceRecord.GetType().Name}",
                        suggestions: new List<string>
                        {
                            "Supported types: Spell, Weapon, Armor, Quest, NPC, Perk, Faction, Book, MiscItem, Global, LeveledItem, FormList, Outfit, Location, EncounterZone"
                        });
            }
        }
        catch (Exception ex)
        {
            return Result<IMajorRecord>.Fail($"Failed to create override record: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Find records across plugins by search pattern
    /// </summary>
    public Result<List<RecordSearchResult>> FindRecords(
        string? searchPattern,
        string? editorId,
        string? recordType,
        string? pluginPath,
        string? dataFolder,
        bool allPlugins = false)
    {
        try
        {
            var results = new List<RecordSearchResult>();
            var pluginsToSearch = new List<string>();

            if (allPlugins && !string.IsNullOrEmpty(dataFolder))
            {
                if (!Directory.Exists(dataFolder))
                {
                    return Result<List<RecordSearchResult>>.Fail(
                        $"Data folder not found: {dataFolder}",
                        suggestions: new List<string>
                        {
                            "Check the data folder path is correct",
                            "Use an absolute path"
                        });
                }

                pluginsToSearch.AddRange(Directory.GetFiles(dataFolder, "*.esp"));
                pluginsToSearch.AddRange(Directory.GetFiles(dataFolder, "*.esm"));
                pluginsToSearch.AddRange(Directory.GetFiles(dataFolder, "*.esl"));
            }
            else if (!string.IsNullOrEmpty(pluginPath))
            {
                if (!File.Exists(pluginPath))
                {
                    return Result<List<RecordSearchResult>>.Fail($"Plugin not found: {pluginPath}");
                }
                pluginsToSearch.Add(pluginPath);
            }
            else
            {
                return Result<List<RecordSearchResult>>.Fail(
                    "Must provide either --plugin or --data-folder with --all-plugins",
                    suggestions: new List<string>
                    {
                        "Use --plugin to search a specific plugin",
                        "Use --data-folder --all-plugins to search all plugins"
                    });
            }

            foreach (var plugin in pluginsToSearch)
            {
                try
                {
                    var mod = SkyrimMod.CreateFromBinaryOverlay(plugin, SkyrimRelease.SkyrimSE);
                    var pluginName = Path.GetFileName(plugin);

                    IEnumerable<IMajorRecordGetter> recordsToSearch = mod.EnumerateMajorRecords();

                    if (!string.IsNullOrEmpty(recordType))
                    {
                        var filtered = FilterByRecordType(mod, recordType);
                        if (filtered == null)
                        {
                            return Result<List<RecordSearchResult>>.Fail(
                                $"Unknown record type '{recordType}'.",
                                null, KnownRecordTypes.Select(t => $"Try --type {t}").ToList());
                        }
                        recordsToSearch = filtered;
                    }

                    foreach (var record in recordsToSearch)
                    {
                        bool matches = false;

                        if (!string.IsNullOrEmpty(editorId))
                        {
                            matches = record.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true;
                        }
                        else if (!string.IsNullOrEmpty(searchPattern))
                        {
                            // Handle wildcard pattern
                            if (searchPattern == "*")
                            {
                                matches = true; // Match all records when wildcard is used
                            }
                            else
                            {
                                matches = (record.EditorID?.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) == true) ||
                                         (GetRecordName(record)?.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) == true);
                            }
                        }

                        if (matches)
                        {
                            results.Add(new RecordSearchResult
                            {
                                PluginName = pluginName,
                                EditorId = record.EditorID ?? string.Empty,
                                FormKey = record.FormKey.ToString(),
                                RecordType = record.GetType().Name.Replace("Getter", "").Replace("ReadOnly", ""),
                                Name = GetRecordName(record)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Skipping plugin {plugin}: {ex.Message}");
                    continue;
                }
            }

            return Result<List<RecordSearchResult>>.Ok(results);
        }
        catch (Exception ex)
        {
            return Result<List<RecordSearchResult>>.Fail(
                $"Failed to search records: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Batch create overrides for multiple records
    /// </summary>
    public Result<BatchOverrideResult> BatchOverride(
        string sourcePluginPath,
        string? recordType,
        string? searchPattern,
        string[]? editorIds,
        string outputPluginName,
        string? dataFolder = null)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
            {
                return Result<BatchOverrideResult>.Fail($"Source plugin not found: {sourcePluginPath}");
            }

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePluginPath, SkyrimRelease.SkyrimSE);

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            patchMod.ModHeader.MasterReferences.Add(new MasterReference
            {
                Master = sourceMod.ModKey
            });

            var recordsToOverride = new List<IMajorRecordGetter>();

            if (editorIds != null && editorIds.Length > 0)
            {
                foreach (var editorId in editorIds)
                {
                    var findResult = FindRecordByEditorId(sourceMod, editorId, recordType ?? "");
                    if (findResult.Success && findResult.Value != null)
                    {
                        recordsToOverride.Add(findResult.Value);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(searchPattern))
            {
                IEnumerable<IMajorRecordGetter>? allRecords = string.IsNullOrEmpty(recordType)
                    ? sourceMod.EnumerateMajorRecords()
                    : FilterByRecordType(sourceMod, recordType);

                if (allRecords == null)
                {
                    return Result<BatchOverrideResult>.Fail(
                        $"Unknown record type '{recordType}'.",
                        null, KnownRecordTypes.Select(t => $"Try --type {t}").ToList());
                }

                foreach (var record in allRecords)
                {
                    if (record.EditorID?.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        recordsToOverride.Add(record);
                    }
                }
            }

            var modifiedRecords = new List<string>();
            foreach (var record in recordsToOverride)
            {
                var overrideResult = CreateOverrideRecord(patchMod, record, false);
                if (overrideResult.Success)
                {
                    modifiedRecords.Add(record.EditorID ?? record.FormKey.ToString());
                }
            }

            var outputDir = !string.IsNullOrEmpty(dataFolder)
                ? dataFolder
                : Path.GetDirectoryName(sourcePluginPath) ?? Directory.GetCurrentDirectory();

            var outputPath = Path.Combine(outputDir, outputPluginName);

            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Created batch override patch: {outputPath}");

            return Result<BatchOverrideResult>.Ok(new BatchOverrideResult
            {
                RecordsModified = modifiedRecords.Count,
                ModifiedRecords = modifiedRecords,
                PatchPath = outputPath
            });
        }
        catch (Exception ex)
        {
            return Result<BatchOverrideResult>.Fail(
                $"Failed to create batch override: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Remove Papyrus script data (VMAD) from one or more records, creating an override patch.
    /// Works on any record type that carries a VirtualMachineAdapter (Book, Weapon, Armor, NPC,
    /// Container, Activator, etc.) - anything implementing Mutagen's IHaveVirtualMachineAdapter.
    /// Records matched by the filter that don't carry script data, or don't have the named script,
    /// are skipped and reported separately rather than failing the whole batch.
    /// </summary>
    /// <param name="scriptName">
    /// If set, only script entries whose Name matches (case-insensitive) are removed - other
    /// scripts on the same record are left alone. If null/empty, the record's entire
    /// VirtualMachineAdapter (all attached scripts) is stripped.
    /// </param>
    public Result<BatchRemoveScriptResult> RemoveScript(
        string sourcePluginPath,
        string? recordType,
        string? searchPattern,
        string[]? editorIds,
        string? scriptName,
        string outputPluginName,
        string? dataFolder = null,
        bool light = false)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
            {
                return Result<BatchRemoveScriptResult>.Fail($"Source plugin not found: {sourcePluginPath}");
            }

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePluginPath, SkyrimRelease.SkyrimSE);

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            patchMod.ModHeader.MasterReferences.Add(new MasterReference
            {
                Master = sourceMod.ModKey
            });

            var recordsToPatch = new List<IMajorRecordGetter>();

            if (editorIds != null && editorIds.Length > 0)
            {
                foreach (var editorId in editorIds)
                {
                    var findResult = FindRecordByEditorId(sourceMod, editorId, recordType ?? "");
                    if (findResult.Success && findResult.Value != null)
                    {
                        recordsToPatch.Add(findResult.Value);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(searchPattern))
            {
                IEnumerable<IMajorRecordGetter>? allRecords = string.IsNullOrEmpty(recordType)
                    ? sourceMod.EnumerateMajorRecords()
                    : FilterByRecordType(sourceMod, recordType);

                if (allRecords == null)
                {
                    return Result<BatchRemoveScriptResult>.Fail(
                        $"Unknown record type '{recordType}'.",
                        null, KnownRecordTypes.Select(t => $"Try --type {t}").ToList());
                }

                foreach (var record in allRecords)
                {
                    if (searchPattern == "*" ||
                        record.EditorID?.Contains(searchPattern, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        recordsToPatch.Add(record);
                    }
                }
            }
            else
            {
                return Result<BatchRemoveScriptResult>.Fail(
                    "Must provide either --editor-ids or --search",
                    suggestions: new List<string>
                    {
                        "Use --editor-ids \"Id1,Id2\" for specific records",
                        "Use --search \"pattern\" --type <type> to match many records"
                    });
            }

            if (recordsToPatch.Count == 0)
            {
                return Result<BatchRemoveScriptResult>.Fail("No matching records found");
            }

            var modifiedRecords = new List<string>();
            var skippedRecords = new List<string>();
            var totalScriptsRemoved = 0;

            foreach (var record in recordsToPatch)
            {
                var recordLabel = record.EditorID ?? record.FormKey.ToString();

                if (record is not IHaveVirtualMachineAdapterGetter sourceVmaGetter ||
                    sourceVmaGetter.VirtualMachineAdapter == null)
                {
                    skippedRecords.Add($"{recordLabel} (no script data)");
                    continue;
                }

                var overrideResult = CreateOverrideRecord(patchMod, record, false);
                if (!overrideResult.Success || overrideResult.Value == null)
                {
                    skippedRecords.Add($"{recordLabel} (unsupported record type)");
                    continue;
                }

                // IHaveVirtualMachineAdapter only exposes a getter - the concrete record types
                // (Book, Weapon, Armor, Npc, ...) have the real settable property, but there's no
                // shared settable interface for it. Reflection lets this work generically across
                // every record type CreateOverrideRecord supports instead of hand-casing each one.
                var majorRecord = overrideResult.Value;
                var vmaProperty = majorRecord.GetType().GetProperty("VirtualMachineAdapter");
                if (vmaProperty?.GetValue(majorRecord) is not AVirtualMachineAdapter currentVma)
                {
                    skippedRecords.Add($"{recordLabel} (no script data)");
                    continue;
                }

                if (string.IsNullOrEmpty(scriptName))
                {
                    totalScriptsRemoved += currentVma.Scripts?.Count ?? 0;
                    vmaProperty.SetValue(majorRecord, null);
                }
                else
                {
                    var scripts = currentVma.Scripts;
                    if (scripts == null || scripts.Count == 0)
                    {
                        skippedRecords.Add($"{recordLabel} (no script data)");
                        continue;
                    }

                    var indicesToRemove = new List<int>();
                    for (var i = 0; i < scripts.Count; i++)
                    {
                        if (string.Equals(scripts[i].Name, scriptName, StringComparison.OrdinalIgnoreCase))
                        {
                            indicesToRemove.Add(i);
                        }
                    }

                    if (indicesToRemove.Count == 0)
                    {
                        skippedRecords.Add($"{recordLabel} (script '{scriptName}' not found)");
                        continue;
                    }

                    foreach (var index in indicesToRemove.OrderByDescending(i => i))
                    {
                        scripts.RemoveAt(index);
                    }

                    totalScriptsRemoved += indicesToRemove.Count;

                    if (scripts.Count == 0)
                    {
                        vmaProperty.SetValue(majorRecord, null);
                    }
                }

                modifiedRecords.Add(recordLabel);
            }

            if (modifiedRecords.Count == 0)
            {
                return Result<BatchRemoveScriptResult>.Fail(
                    "No records were modified - every matched record was skipped",
                    suggestions: skippedRecords.Take(5).ToList());
            }

            if (light)
            {
                patchMod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
            }

            var outputDir = !string.IsNullOrEmpty(dataFolder)
                ? dataFolder
                : Path.GetDirectoryName(sourcePluginPath) ?? Directory.GetCurrentDirectory();

            var outputPath = Path.Combine(outputDir, outputPluginName);

            patchMod.WriteToBinary(outputPath);

            _logger.Info(
                $"Created script-removal patch: {outputPath} ({modifiedRecords.Count} record(s), {totalScriptsRemoved} script(s) removed)");

            return Result<BatchRemoveScriptResult>.Ok(new BatchRemoveScriptResult
            {
                RecordsModified = modifiedRecords.Count,
                ScriptsRemoved = totalScriptsRemoved,
                ModifiedRecords = modifiedRecords,
                SkippedRecords = skippedRecords,
                PatchPath = outputPath
            });
        }
        catch (Exception ex)
        {
            return Result<BatchRemoveScriptResult>.Fail($"Failed to remove script: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Batch create overrides for multiple weapon records, appending a suffix to their display Name.
    /// Records that can't be found or aren't weapons are skipped (reported separately from modified).
    /// </summary>
    public Result<BatchOverrideResult> BatchAppendNameSuffix(
        string sourcePluginPath,
        string[] editorIds,
        string recordType,
        string suffix,
        string outputPluginName,
        string? dataFolder = null)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
            {
                return Result<BatchOverrideResult>.Fail($"Source plugin not found: {sourcePluginPath}");
            }

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePluginPath, SkyrimRelease.SkyrimSE);

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            patchMod.ModHeader.MasterReferences.Add(new MasterReference
            {
                Master = sourceMod.ModKey
            });

            var modifiedRecords = new List<string>();
            var skipped = new List<string>();

            foreach (var editorId in editorIds)
            {
                var findResult = FindRecordByEditorId(sourceMod, editorId, recordType);
                if (!findResult.Success || findResult.Value == null)
                {
                    skipped.Add(editorId);
                    continue;
                }

                if (findResult.Value is not IWeaponGetter weapon)
                {
                    skipped.Add(editorId);
                    continue;
                }

                var weaponOverride = (Weapon)weapon.DeepCopy();
                var currentName = weaponOverride.Name?.String ?? "";
                weaponOverride.Name = currentName + suffix;
                patchMod.Weapons.Add(weaponOverride);
                modifiedRecords.Add(editorId);
            }

            var outputDir = !string.IsNullOrEmpty(dataFolder)
                ? dataFolder
                : Path.GetDirectoryName(sourcePluginPath) ?? Directory.GetCurrentDirectory();

            var outputPath = Path.Combine(outputDir, outputPluginName);

            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Created batch rename patch: {outputPath}");

            if (skipped.Count > 0)
            {
                _logger.Info($"Skipped {skipped.Count} EditorID(s) not found or not weapons: {string.Join(", ", skipped)}");
            }

            return Result<BatchOverrideResult>.Ok(new BatchOverrideResult
            {
                RecordsModified = modifiedRecords.Count,
                ModifiedRecords = modifiedRecords,
                PatchPath = outputPath
            });
        }
        catch (Exception ex)
        {
            return Result<BatchOverrideResult>.Fail(
                $"Failed to create batch rename: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Filter records by type
    /// </summary>
    /// <summary>
    /// Record type names accepted by --type, for error messages.
    /// </summary>
    public static readonly string[] KnownRecordTypes =
    {
        "spell", "weapon", "armor", "armoraddon", "quest", "npc", "perk", "faction", "book",
        "miscitem", "global", "leveleditem", "levelednpc", "formlist", "outfit", "location",
        "encounterzone", "magiceffect", "ingestible", "ingredient", "keyword", "container",
        "cell", "worldspace", "objecteffect", "constructibleobject", "soulgem", "ammunition",
        "scroll", "activator", "door", "flora", "furniture", "light", "static", "idle",
        "shout", "wordofpower", "race", "class", "headpart", "sound", "explosion", "projectile",
        "region", "impactdataset", "texture", "message", "packagelist", "combatstyle", "package"
    };

    /// <summary>
    /// Resolves a --type name to the matching record group.
    ///
    /// Returns null for an unrecognised type so the caller can report it. This used to fall
    /// through to EnumerateMajorRecords(), which silently returned EVERY record in the plugin -
    /// so a typo, or any type that simply wasn't in the list, looked like a successful query
    /// with an absurd result (--type MGEF against Skyrim.esm reported 869,687 "matches"). A
    /// filter that silently ignores the filter is worse than one that refuses.
    ///
    /// Names are matched case-insensitively, and the common record-signature spellings (MGEF,
    /// ALCH, KYWD...) are accepted alongside the long names, since that's what people actually
    /// type when reading xEdit or the wiki.
    /// </summary>
    private IEnumerable<IMajorRecordGetter>? FilterByRecordType(ISkyrimModGetter mod, string recordType)
    {
        return recordType.ToLowerInvariant() switch
        {
            "spell" or "spel" => mod.Spells,
            "weapon" or "weap" => mod.Weapons,
            "armor" or "armo" => mod.Armors,
            "armoraddon" or "arma" => mod.ArmorAddons,
            "quest" or "qust" => mod.Quests,
            "npc" or "npc_" => mod.Npcs,
            "perk" => mod.Perks,
            "faction" or "fact" => mod.Factions,
            "book" or "book_" => mod.Books,
            "miscitem" or "misc" => mod.MiscItems,
            "global" or "glob" => mod.Globals,
            "leveleditem" or "lvli" => mod.LeveledItems,
            "levelednpc" or "lvln" => mod.LeveledNpcs,
            "formlist" or "flst" => mod.FormLists,
            "outfit" or "otft" => mod.Outfits,
            "location" or "lctn" => mod.Locations,
            "encounterzone" or "eczn" => mod.EncounterZones,
            "magiceffect" or "mgef" => mod.MagicEffects,
            "ingestible" or "alch" or "potion" => mod.Ingestibles,
            "ingredient" or "ingr" => mod.Ingredients,
            "keyword" or "kywd" => mod.Keywords,
            "container" or "cont" => mod.Containers,
            "objecteffect" or "ench" or "enchantment" => mod.ObjectEffects,
            "constructibleobject" or "cobj" or "recipe" => mod.ConstructibleObjects,
            "soulgem" or "slgm" => mod.SoulGems,
            "ammunition" or "ammo" => mod.Ammunitions,
            "scroll" or "scrl" => mod.Scrolls,
            "activator" or "acti" => mod.Activators,
            "door" => mod.Doors,
            "flora" => mod.Florae,
            "furniture" or "furn" => mod.Furniture,
            "light" or "ligh" => mod.Lights,
            "static" or "stat" => mod.Statics,
            "idle" or "idlm" => mod.IdleMarkers,
            "shout" or "shou" => mod.Shouts,
            "wordofpower" or "wood" => mod.WordsOfPower,
            "race" => mod.Races,
            "class" or "clas" => mod.Classes,
            "headpart" or "hdpt" => mod.HeadParts,
            "sound" or "soun" => mod.SoundMarkers,
            "explosion" or "expl" => mod.Explosions,
            "projectile" or "proj" => mod.Projectiles,
            "region" or "regn" => mod.Regions,
            "impactdataset" or "ipds" => mod.ImpactDataSets,
            "texture" or "txst" or "textureset" => mod.TextureSets,
            "message" or "mesg" => mod.Messages,
            "combatstyle" or "csty" => mod.CombatStyles,
            "package" or "pack" => mod.Packages,
            _ => null
        };
    }

    /// <summary>
    /// Get the display name of a record
    /// </summary>
    private string? GetRecordName(IMajorRecordGetter record)
    {
        return record switch
        {
            ISpellGetter spell => spell.Name?.String,
            IWeaponGetter weapon => weapon.Name?.String,
            IArmorGetter armor => armor.Name?.String,
            IQuestGetter quest => quest.Name?.String,
            INpcGetter npc => npc.Name?.String,
            IPerkGetter perk => perk.Name?.String,
            IBookGetter book => book.Name?.String,
            _ => null
        };
    }

    /// <summary>
    /// Compare two versions of the same record
    /// </summary>
    public Result<RecordComparison> CompareRecords(
        string plugin1Path,
        string plugin2Path,
        string? editorId,
        string? formId,
        string? recordType)
    {
        try
        {
            if (!File.Exists(plugin1Path))
            {
                return Result<RecordComparison>.Fail($"Plugin 1 not found: {plugin1Path}");
            }

            if (!File.Exists(plugin2Path))
            {
                return Result<RecordComparison>.Fail($"Plugin 2 not found: {plugin2Path}");
            }

            var mod1 = SkyrimMod.CreateFromBinaryOverlay(plugin1Path, SkyrimRelease.SkyrimSE);
            var mod2 = SkyrimMod.CreateFromBinaryOverlay(plugin2Path, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? record1 = null;
            IMajorRecordGetter? record2 = null;

            if (!string.IsNullOrEmpty(formId))
            {
                var findResult1 = FindRecordByFormKey(mod1, formId);
                var findResult2 = FindRecordByFormKey(mod2, formId);

                if (!findResult1.Success || !findResult2.Success)
                {
                    return Result<RecordComparison>.Fail(
                        "Record not found in one or both plugins",
                        suggestions: new List<string>
                        {
                            "Verify the FormKey exists in both plugins",
                            "Use --editor-id if the record has different FormKeys"
                        });
                }

                record1 = findResult1.Value;
                record2 = findResult2.Value;
            }
            else if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(recordType))
            {
                var findResult1 = FindRecordByEditorId(mod1, editorId, recordType);
                var findResult2 = FindRecordByEditorId(mod2, editorId, recordType);

                if (!findResult1.Success || !findResult2.Success)
                {
                    return Result<RecordComparison>.Fail(
                        "Record not found in one or both plugins",
                        suggestions: new List<string>
                        {
                            "Verify the EditorID exists in both plugins",
                            "Check the record type is correct"
                        });
                }

                record1 = findResult1.Value;
                record2 = findResult2.Value;
            }
            else
            {
                return Result<RecordComparison>.Fail(
                    "Must provide either FormID or both EditorID and RecordType");
            }

            if (record1 == null || record2 == null)
            {
                return Result<RecordComparison>.Fail("Failed to load records for comparison");
            }

            var info1Result = ExtractRecordInfo(record1, false);
            var info2Result = ExtractRecordInfo(record2, false);

            if (!info1Result.Success || !info2Result.Success)
            {
                return Result<RecordComparison>.Fail("Failed to extract record properties");
            }

            var info1 = info1Result.Value!;
            var info2 = info2Result.Value!;

            var differences = new Dictionary<string, FieldDifference>();

            var allKeys = info1.Properties.Keys.Union(info2.Properties.Keys).ToHashSet();

            foreach (var key in allKeys)
            {
                var value1 = info1.Properties.GetValueOrDefault(key);
                var value2 = info2.Properties.GetValueOrDefault(key);

                var value1Str = value1?.ToString() ?? "";
                var value2Str = value2?.ToString() ?? "";

                if (value1Str != value2Str)
                {
                    differences[key] = new FieldDifference
                    {
                        Field = key,
                        OriginalValue = value1,
                        ModifiedValue = value2
                    };
                }
            }

            return Result<RecordComparison>.Ok(new RecordComparison
            {
                Original = info1,
                Modified = info2,
                Differences = differences
            });
        }
        catch (Exception ex)
        {
            return Result<RecordComparison>.Fail(
                $"Failed to compare records: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Detect load order conflicts for a record or plugin
    /// </summary>
    public Result<ConflictReport> DetectConflicts(
        string? pluginPath,
        string? editorId,
        string? formId,
        string? recordType,
        string dataFolder)
    {
        try
        {
            if (!Directory.Exists(dataFolder))
            {
                return Result<ConflictReport>.Fail($"Data folder not found: {dataFolder}");
            }

            if (string.IsNullOrEmpty(pluginPath) && string.IsNullOrEmpty(formId) && string.IsNullOrEmpty(editorId))
            {
                return Result<ConflictReport>.Fail(
                    "Must provide either --plugin, --form-id, or --editor-id");
            }

            var allPlugins = new List<string>();
            allPlugins.AddRange(Directory.GetFiles(dataFolder, "*.esm").OrderBy(f => f));
            allPlugins.AddRange(Directory.GetFiles(dataFolder, "*.esp").OrderBy(f => f));
            allPlugins.AddRange(Directory.GetFiles(dataFolder, "*.esl").OrderBy(f => f));

            Mutagen.Bethesda.Plugins.FormKey? targetFormKey = null;
            string targetEditorId = editorId ?? "";

            if (!string.IsNullOrEmpty(formId))
            {
                if (!Mutagen.Bethesda.Plugins.FormKey.TryFactory(formId, out var parsedFormKey))
                {
                    return Result<ConflictReport>.Fail($"Invalid FormID format: {formId}");
                }
                targetFormKey = parsedFormKey;
            }

            var conflicts = new List<ConflictingPlugin>();
            int loadOrder = 0;

            foreach (var plugin in allPlugins)
            {
                try
                {
                    var mod = SkyrimMod.CreateFromBinaryOverlay(plugin, SkyrimRelease.SkyrimSE);

                    bool hasRecord = false;

                    if (targetFormKey.HasValue)
                    {
                        hasRecord = mod.EnumerateMajorRecords().Any(r => r.FormKey == targetFormKey.Value);
                    }
                    else if (!string.IsNullOrEmpty(targetEditorId) && !string.IsNullOrEmpty(recordType))
                    {
                        var findResult = FindRecordByEditorId(mod, targetEditorId, recordType);
                        hasRecord = findResult.Success;

                        if (hasRecord && !targetFormKey.HasValue && findResult.Value != null)
                        {
                            targetFormKey = findResult.Value.FormKey;
                            targetEditorId = findResult.Value.EditorID ?? targetEditorId;
                        }
                    }
                    else if (!string.IsNullOrEmpty(pluginPath))
                    {
                        hasRecord = Path.GetFileName(plugin).Equals(
                            Path.GetFileName(pluginPath),
                            StringComparison.OrdinalIgnoreCase);
                    }

                    if (hasRecord)
                    {
                        conflicts.Add(new ConflictingPlugin
                        {
                            PluginName = Path.GetFileName(plugin),
                            LoadOrder = loadOrder,
                            IsWinner = false
                        });
                    }

                    loadOrder++;
                }
                catch
                {
                    loadOrder++;
                    continue;
                }
            }

            if (conflicts.Count == 0)
            {
                return Result<ConflictReport>.Fail(
                    "No conflicts found",
                    suggestions: new List<string>
                    {
                        "Record may not exist in any loaded plugins",
                        "Verify the FormKey or EditorID is correct"
                    });
            }

            conflicts.Last().IsWinner = true;

            return Result<ConflictReport>.Ok(new ConflictReport
            {
                FormKey = targetFormKey?.ToString() ?? "",
                EditorId = targetEditorId,
                Conflicts = conflicts,
                WinningPlugin = conflicts.Last().PluginName
            });
        }
        catch (Exception ex)
        {
            return Result<ConflictReport>.Fail(
                $"Failed to detect conflicts: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Extract RecordInfo from a record (helper for compare)
    /// </summary>
    private Result<RecordInfo> ExtractRecordInfo(IMajorRecordGetter record, bool includeRaw)
    {
        var recordInfo = new RecordInfo
        {
            EditorId = record.EditorID ?? string.Empty,
            FormKey = record.FormKey.ToString(),
            RecordType = record.GetType().Name.Replace("Getter", "").Replace("ReadOnly", "")
        };

        var propsResult = ExtractRecordProperties(record, includeRaw);
        if (!propsResult.Success)
        {
            return Result<RecordInfo>.Fail(propsResult.Error ?? "Failed to extract properties");
        }

        recordInfo.Properties = propsResult.Value ?? new Dictionary<string, object?>();

        return Result<RecordInfo>.Ok(recordInfo);
    }

    /// <summary>
    /// List all conditions on a record
    /// </summary>
    public Result<List<ConditionInfo>> ListConditions(
        string pluginPath,
        string? editorId,
        string? formId,
        string? recordType)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result<List<ConditionInfo>>.Fail($"Plugin not found: {pluginPath}");
            }

            var mod = SkyrimMod.CreateFromBinaryOverlay(pluginPath, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? record = null;

            if (!string.IsNullOrEmpty(formId))
            {
                var findResult = FindRecordByFormKey(mod, formId);
                if (!findResult.Success || findResult.Value == null)
                {
                    return Result<List<ConditionInfo>>.Fail("Record not found");
                }
                record = findResult.Value;
            }
            else if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(recordType))
            {
                var findResult = FindRecordByEditorId(mod, editorId, recordType);
                if (!findResult.Success || findResult.Value == null)
                {
                    return Result<List<ConditionInfo>>.Fail("Record not found");
                }
                record = findResult.Value;
            }
            else
            {
                return Result<List<ConditionInfo>>.Fail("Must provide either FormID or both EditorID and RecordType");
            }

            return ExtractConditions(record);
        }
        catch (Exception ex)
        {
            return Result<List<ConditionInfo>>.Fail($"Failed to list conditions: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Remove specific conditions from a record by index
    /// </summary>
    public Result<string> RemoveConditions(
        string sourcePluginPath,
        string? editorId,
        string? formId,
        string? recordType,
        int[] conditionIndices,
        string outputPluginName,
        string? dataFolder = null)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
            {
                return Result<string>.Fail($"Source plugin not found: {sourcePluginPath}");
            }

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePluginPath, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? sourceRecord = null;

            if (!string.IsNullOrEmpty(formId))
            {
                var findResult = FindRecordByFormKey(sourceMod, formId);
                if (!findResult.Success || findResult.Value == null)
                {
                    return Result<string>.Fail("Record not found");
                }
                sourceRecord = findResult.Value;
            }
            else if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(recordType))
            {
                var findResult = FindRecordByEditorId(sourceMod, editorId, recordType);
                if (!findResult.Success || findResult.Value == null)
                {
                    return Result<string>.Fail("Record not found");
                }
                sourceRecord = findResult.Value;
            }
            else
            {
                return Result<string>.Fail("Must provide either FormID or both EditorID and RecordType");
            }

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            patchMod.ModHeader.MasterReferences.Add(new MasterReference
            {
                Master = sourceMod.ModKey
            });

            // Only Perks are currently supported for condition manipulation
            if (sourceRecord is not IPerkGetter)
            {
                return Result<string>.Fail(
                    "Condition manipulation currently only supported for Perk records",
                    suggestions: new List<string>
                    {
                        "Use --type perk",
                        "Other record types (Package, IdleAnimation) coming soon"
                    });
            }

            var perkSource = (IPerkGetter)sourceRecord;
            var perkOverride = (Perk)perkSource.DeepCopy();

            if (perkOverride.Conditions == null || perkOverride.Conditions.Count == 0)
            {
                return Result<string>.Fail("Record has no conditions to remove");
            }

            // Sort indices in descending order to avoid index shifting issues
            var sortedIndices = conditionIndices.OrderByDescending(i => i).ToArray();

            foreach (var index in sortedIndices)
            {
                if (index < 0 || index >= perkOverride.Conditions.Count)
                {
                    return Result<string>.Fail($"Invalid condition index: {index}. Record has {perkOverride.Conditions.Count} conditions.");
                }
                perkOverride.Conditions.RemoveAt(index);
            }

            patchMod.Perks.Add(perkOverride);

            var outputDir = !string.IsNullOrEmpty(dataFolder)
                ? dataFolder
                : Path.GetDirectoryName(sourcePluginPath) ?? Directory.GetCurrentDirectory();

            var outputPath = Path.Combine(outputDir, outputPluginName);

            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Created patch with {conditionIndices.Length} condition(s) removed: {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to remove conditions: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Add a condition to a record
    /// </summary>
    public Result<string> AddCondition(
        string sourcePluginPath,
        string? editorId,
        string? formId,
        string? recordType,
        string conditionFunction,
        float comparisonValue,
        string comparisonOperator,
        string outputPluginName,
        string? dataFolder = null)
    {
        try
        {
            if (!File.Exists(sourcePluginPath))
            {
                return Result<string>.Fail($"Source plugin not found: {sourcePluginPath}");
            }

            var sourceMod = SkyrimMod.CreateFromBinaryOverlay(sourcePluginPath, SkyrimRelease.SkyrimSE);

            IMajorRecordGetter? sourceRecord = null;

            if (!string.IsNullOrEmpty(formId))
            {
                var findResult = FindRecordByFormKey(sourceMod, formId);
                if (!findResult.Success || findResult.Value == null)
                {
                    return Result<string>.Fail("Record not found");
                }
                sourceRecord = findResult.Value;
            }
            else if (!string.IsNullOrEmpty(editorId) && !string.IsNullOrEmpty(recordType))
            {
                var findResult = FindRecordByEditorId(sourceMod, editorId, recordType);
                if (!findResult.Success || findResult.Value == null)
                {
                    return Result<string>.Fail("Record not found");
                }
                sourceRecord = findResult.Value;
            }
            else
            {
                return Result<string>.Fail("Must provide either FormID or both EditorID and RecordType");
            }

            if (!outputPluginName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !outputPluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                outputPluginName += ".esp";
            }

            var outputModKey = ModKey.FromFileName(outputPluginName);
            var patchMod = new SkyrimMod(outputModKey, SkyrimRelease.SkyrimSE);

            patchMod.ModHeader.MasterReferences.Add(new MasterReference
            {
                Master = sourceMod.ModKey
            });

            // Only Perks are currently supported
            if (sourceRecord is not IPerkGetter)
            {
                return Result<string>.Fail(
                    "Condition manipulation currently only supported for Perk records",
                    suggestions: new List<string>
                    {
                        "Use --type perk",
                        "Other record types coming soon"
                    });
            }

            var perkSource = (IPerkGetter)sourceRecord;
            var perkOverride = (Perk)perkSource.DeepCopy();

            // Parse comparison operator
            if (!Enum.TryParse<CompareOperator>(comparisonOperator, true, out var compareOp))
            {
                return Result<string>.Fail(
                    $"Invalid comparison operator: {comparisonOperator}",
                    suggestions: new List<string>
                    {
                        "Valid operators: EqualTo, NotEqualTo, GreaterThan, GreaterThanOrEqualTo, LessThan, LessThanOrEqualTo"
                    });
            }

            // Create condition based on function name
            var conditionResult = CreateConditionFromFunction(conditionFunction, comparisonValue, compareOp);
            if (!conditionResult.Success || conditionResult.Value == null)
            {
                return Result<string>.Fail(conditionResult.Error ?? "Failed to create condition");
            }

            perkOverride.Conditions.Add(conditionResult.Value);

            patchMod.Perks.Add(perkOverride);

            var outputDir = !string.IsNullOrEmpty(dataFolder)
                ? dataFolder
                : Path.GetDirectoryName(sourcePluginPath) ?? Directory.GetCurrentDirectory();

            var outputPath = Path.Combine(outputDir, outputPluginName);

            patchMod.WriteToBinary(outputPath);

            _logger.Info($"Created patch with new condition: {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to add condition: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Create a condition from a function name
    /// Uses reflection to create ConditionData types dynamically
    /// </summary>
    private Result<Condition> CreateConditionFromFunction(
        string functionName,
        float comparisonValue,
        CompareOperator compareOperator)
    {
        try
        {
            // Build the ConditionData type name
            var typeName = $"{functionName}ConditionData";
            var fullTypeName = $"Mutagen.Bethesda.Skyrim.{typeName}";

            // Try to find the type in the Mutagen assembly
            var conditionDataType = typeof(ISkyrimMod).Assembly.GetType(fullTypeName);
            if (conditionDataType == null)
            {
                return Result<Condition>.Fail(
                    $"Unknown condition function: {functionName}",
                    suggestions: new List<string>
                    {
                        "Use exact Mutagen function names (e.g., GetLevel, GetActorValue)",
                        "Supported simple functions: GetLevel",
                        "See Mutagen documentation for full list of condition functions"
                    });
            }

            // Create instance of ConditionData
            var conditionData = System.Activator.CreateInstance(conditionDataType) as ConditionData;
            if (conditionData == null)
            {
                return Result<Condition>.Fail($"Failed to create instance of {typeName}");
            }

            // Create ConditionFloat with the data
            var condition = new ConditionFloat
            {
                ComparisonValue = comparisonValue,
                CompareOperator = compareOperator,
                Data = conditionData
            };

            return Result<Condition>.Ok(condition);
        }
        catch (Exception ex)
        {
            return Result<Condition>.Fail($"Failed to create condition: {ex.Message}", ex.StackTrace);
        }
    }

    /// <summary>
    /// Remove a record from a plugin by EditorID.
    /// </summary>
    public Result RemoveRecord(SkyrimMod mod, string editorId)
    {
        try
        {
            // Search all record groups for the EditorID
            bool removed = TryRemoveFromGroup(mod.Weapons, editorId)
                || TryRemoveFromGroup(mod.Armors, editorId)
                || TryRemoveFromGroup(mod.Spells, editorId)
                || TryRemoveFromGroup(mod.Quests, editorId)
                || TryRemoveFromGroup(mod.Npcs, editorId)
                || TryRemoveFromGroup(mod.Perks, editorId)
                || TryRemoveFromGroup(mod.Factions, editorId)
                || TryRemoveFromGroup(mod.Books, editorId)
                || TryRemoveFromGroup(mod.MiscItems, editorId)
                || TryRemoveFromGroup(mod.Globals, editorId)
                || TryRemoveFromGroup(mod.LeveledItems, editorId)
                || TryRemoveFromGroup(mod.FormLists, editorId)
                || TryRemoveFromGroup(mod.Outfits, editorId)
                || TryRemoveFromGroup(mod.Locations, editorId)
                || TryRemoveFromGroup(mod.EncounterZones, editorId)
                || TryRemoveFromGroup(mod.Packages, editorId)
                || TryRemoveFromGroup(mod.ObjectEffects, editorId)
                || TryRemoveFromGroup(mod.MagicEffects, editorId)
                || TryRemoveFromGroup(mod.Keywords, editorId)
                || TryRemoveFromGroup(mod.ArmorAddons, editorId)
                || TryRemoveFromGroup(mod.Containers, editorId)
                || TryRemoveFromGroup(mod.ConstructibleObjects, editorId);

            if (!removed)
            {
                return Result.Fail(
                    $"Record not found: {editorId}",
                    suggestions: new List<string>
                    {
                        "Check the EditorID is correct (case-insensitive)",
                        "Use 'esp list-records' to see available records",
                        "Use 'esp find-record --search' to search by pattern"
                    });
            }

            _logger.Info($"Removed record: {editorId}");
            return Result.Ok($"Removed record: {editorId}");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to remove record: {ex.Message}", ex.StackTrace);
        }
    }

    private static bool TryRemoveFromGroup<T>(IGroup<T> group, string editorId)
        where T : class, IMajorRecord, IMajorRecordGetter
    {
        var record = group.FirstOrDefault(r =>
            r.EditorID?.Equals(editorId, StringComparison.OrdinalIgnoreCase) == true);
        if (record != null)
        {
            group.Remove(record.FormKey);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clone a record within a plugin, creating a copy with a new EditorID and FormKey.
    /// </summary>
    public Result<RecordSearchResult> CloneRecord(SkyrimMod mod, string sourceEditorId, string newEditorId)
    {
        try
        {
            // Find the source record
            var sourceRecord = mod.EnumerateMajorRecords()
                .FirstOrDefault(r => r.EditorID?.Equals(sourceEditorId, StringComparison.OrdinalIgnoreCase) == true);

            if (sourceRecord == null)
            {
                return Result<RecordSearchResult>.Fail(
                    $"Source record not found: {sourceEditorId}",
                    suggestions: new List<string>
                    {
                        "Check the EditorID is correct",
                        "Use 'esp find-record --search' to search for the record"
                    });
            }

            // Deep copy and add to the appropriate group with new FormKey
            var clone = sourceRecord.DeepCopy();
            var mutableClone = (IMajorRecord)clone;
            mutableClone.EditorID = newEditorId;

            // Add clone to the correct group based on type
            var formKey = AddCloneToGroup(mod, mutableClone);
            if (formKey == null)
            {
                return Result<RecordSearchResult>.Fail(
                    $"Unsupported record type for cloning: {sourceRecord.GetType().Name}",
                    suggestions: new List<string>
                    {
                        "Supported types: Weapon, Armor, Spell, Quest, NPC, Perk, Faction, Book, Global, LeveledItem, FormList, Outfit, Location, EncounterZone, Package"
                    });
            }

            _logger.Info($"Cloned record: {sourceEditorId} -> {newEditorId} ({formKey})");
            return Result<RecordSearchResult>.Ok(new RecordSearchResult
            {
                EditorId = newEditorId,
                FormKey = formKey.Value.ToString(),
                RecordType = sourceRecord.GetType().Name.Replace("Getter", ""),
                PluginName = mod.ModKey.FileName,
                Name = GetRecordName(sourceRecord)
            });
        }
        catch (Exception ex)
        {
            return Result<RecordSearchResult>.Fail($"Failed to clone record: {ex.Message}", ex.StackTrace);
        }
    }

    private FormKey? AddCloneToGroup(SkyrimMod mod, IMajorRecord clone)
    {
        // Mutagen's FormKey is immutable after creation, so we need to use
        // the Duplicate method which creates a new record with a new FormKey
        var newFormKey = mod.GetNextFormKey();
        switch (clone)
        {
            case Weapon w: var nw = mod.Weapons.AddNew(); CopyRecordFields(w, nw); nw.EditorID = clone.EditorID; return nw.FormKey;
            case Armor a: var na = mod.Armors.AddNew(); CopyRecordFields(a, na); na.EditorID = clone.EditorID; return na.FormKey;
            case Spell s: var ns = mod.Spells.AddNew(); CopyRecordFields(s, ns); ns.EditorID = clone.EditorID; return ns.FormKey;
            case Quest q: var nq = mod.Quests.AddNew(); CopyRecordFields(q, nq); nq.EditorID = clone.EditorID; return nq.FormKey;
            case Npc n: var nn = mod.Npcs.AddNew(); CopyRecordFields(n, nn); nn.EditorID = clone.EditorID; return nn.FormKey;
            case Perk p: var np = mod.Perks.AddNew(); CopyRecordFields(p, np); np.EditorID = clone.EditorID; return np.FormKey;
            case Faction f: var nf = new Faction(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE); CopyRecordFields(f, nf); nf.EditorID = clone.EditorID; mod.Factions.Add(nf); return nf.FormKey;
            case Book b: var nb = mod.Books.AddNew(); CopyRecordFields(b, nb); nb.EditorID = clone.EditorID; return nb.FormKey;
            case Global g: var ng = mod.Globals.AddNewFloat(clone.EditorID ?? "Clone"); CopyRecordFields(g, ng); ng.EditorID = clone.EditorID; return ng.FormKey;
            case LeveledItem li: var nli = mod.LeveledItems.AddNew(); CopyRecordFields(li, nli); nli.EditorID = clone.EditorID; return nli.FormKey;
            case FormList fl: var nfl = mod.FormLists.AddNew(); CopyRecordFields(fl, nfl); nfl.EditorID = clone.EditorID; return nfl.FormKey;
            case Outfit o: var no = mod.Outfits.AddNew(); CopyRecordFields(o, no); no.EditorID = clone.EditorID; return no.FormKey;
            case Location l: var nl = mod.Locations.AddNew(); CopyRecordFields(l, nl); nl.EditorID = clone.EditorID; return nl.FormKey;
            case EncounterZone ez: var nez = mod.EncounterZones.AddNew(); CopyRecordFields(ez, nez); nez.EditorID = clone.EditorID; return nez.FormKey;
            case Package pk: var npk = mod.Packages.AddNew(); CopyRecordFields(pk, npk); npk.EditorID = clone.EditorID; return npk.FormKey;
            default: return null;
        }
    }

    /// <summary>
    /// Copy writable properties from source to destination using reflection.
    /// Skips FormKey and EditorID (set separately).
    /// </summary>
    private static void CopyRecordFields(IMajorRecord source, IMajorRecord dest)
    {
        var type = source.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (prop.Name is "FormKey" or "EditorID") continue;

            try
            {
                var value = prop.GetValue(source);
                prop.SetValue(dest, value);
            }
            catch
            {
                // Skip properties that can't be copied (readonly, indexers, etc.)
            }
        }
    }

    /// <summary>
    /// Add a reference alias to a quest, optionally with a script attached.
    /// Sets ID, Name, and Flags. If a script is provided, it's stored in
    /// QuestFragmentAlias within the quest's VirtualMachineAdapter (the only
    /// way Creation Kit recognizes alias scripts).
    /// </summary>
    public Result<QuestAlias> AddAliasToQuest(
        Quest quest,
        string aliasName,
        string? scriptName = null,
        QuestAlias.Flag? flags = null,
        string? forcedReference = null)
    {
        if (quest.Aliases.Any(a => a.Name == aliasName))
        {
            return Result<QuestAlias>.Fail($"Alias already exists: {aliasName}");
        }

        // Pick the next available alias ID (uint, monotonic)
        var nextId = quest.Aliases.Count == 0 ? 0u : quest.Aliases.Max(a => a.ID) + 1;

        var alias = new QuestAlias
        {
            ID = nextId,
            Name = aliasName,
            Flags = flags
        };

        // A forced reference (ALFR) pins the alias to a specific placed reference - most
        // commonly the player (000014:Skyrim.esm). Without any fill method an alias can fail
        // to fill, which in Skyrim can stop the whole quest from starting, so this matters for
        // "always-on" utility quests (e.g. an MCM config quest using SKI_PlayerLoadGameAlias).
        if (!string.IsNullOrWhiteSpace(forcedReference))
        {
            if (!FormKey.TryFactory(forcedReference, out var refKey))
            {
                return Result<QuestAlias>.Fail(
                    $"Invalid forced reference FormKey: '{forcedReference}'. Expected 'FORMID:MODNAME' with 6 hex digits, e.g. '000014:Skyrim.esm'.");
            }
            alias.ForcedReference.SetTo(refKey);
        }

        quest.Aliases.Add(alias);

        if (!string.IsNullOrEmpty(scriptName))
        {
            var scriptResult = AttachScriptToAlias(quest, alias, scriptName);
            if (!scriptResult.Success)
            {
                // Roll back the alias we just added so the plugin isn't left half-modified
                quest.Aliases.Remove(alias);
                return Result<QuestAlias>.Fail(scriptResult.Error!);
            }
        }

        return Result<QuestAlias>.Ok(alias);
    }

    /// <summary>
    /// Attach a script to an existing alias by name. Creates a QuestFragmentAlias
    /// entry in the quest's VirtualMachineAdapter if one doesn't exist. The
    /// fragment alias's Property.Object MUST reference the quest's FormKey or
    /// the Creation Kit won't see the script.
    /// </summary>
    public Result<ScriptEntry> AttachScriptToAliasByName(Quest quest, string aliasName, string scriptName)
    {
        var alias = quest.Aliases.FirstOrDefault(a => a.Name == aliasName);
        if (alias == null)
        {
            return Result<ScriptEntry>.Fail($"Alias not found: {aliasName}");
        }
        return AttachScriptToAlias(quest, alias, scriptName);
    }

    private Result<ScriptEntry> AttachScriptToAlias(Quest quest, QuestAlias alias, string scriptName)
    {
        var adapter = quest.VirtualMachineAdapter as QuestAdapter ?? new QuestAdapter();
        var aliasIndex = (short)alias.ID;

        // Find or create the QuestFragmentAlias for this alias
        var fragAlias = adapter.Aliases!.FirstOrDefault(fa => fa.Property?.Alias == aliasIndex);
        if (fragAlias == null)
        {
            fragAlias = new QuestFragmentAlias
            {
                Property = new ScriptObjectProperty
                {
                    Object = quest.FormKey.ToLink<ISkyrimMajorRecordGetter>(),
                    Alias = aliasIndex,
                    Flags = ScriptProperty.Flag.Edited
                }
            };
            adapter.Aliases!.Add(fragAlias);
        }

        if (fragAlias.Scripts!.Any(s => string.Equals(s.Name, scriptName, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<ScriptEntry>.Fail($"Script already attached to alias '{alias.Name}': {scriptName}");
        }

        var script = new ScriptEntry
        {
            Name = scriptName,
            Flags = ScriptEntry.Flag.Local
        };
        fragAlias.Scripts!.Add(script);

        // Make sure the adapter is wired back to the quest in case we created a new one
        quest.VirtualMachineAdapter = adapter;

        return Result<ScriptEntry>.Ok(script);
    }
}
