using System.CommandLine;
using System.Text.Json;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace SpookysAutomod.Cli.Commands;

public static class EspLightCommands
{
    public static Command CreateCompactLightCommand(Option<bool> jsonOption)
    {
        var pluginArg = new Argument<string>("plugin", "Path to the ESP to compact and flag");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output path for the compacted plugin (must keep the source filename)")
        {
            IsRequired = true
        };

        var cmd = new Command("compact-light", "Remap plugin-owned FormIDs into the ESL range and set the ESL flag; may affect existing saves and dependent files")
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

                var mod = SkyrimMod.CreateFromBinary(sourcePath, SkyrimRelease.SkyrimSE);
                var sourceRecords = mod.EnumerateMajorRecords().ToArray();
                var localRecords = sourceRecords.Where(r => r.FormKey.ModKey == mod.ModKey).ToArray();
                var overflowRecords = localRecords
                    .Where(r => r.FormKey.ID > 0xFFF)
                    .OrderBy(r => r.FormKey.ID)
                    .ToArray();
                if (overflowRecords.Length == 0)
                    throw new InvalidOperationException("No compaction is needed: all plugin-owned FormIDs already fit 0x000-0xFFF. Use esp flag-light to set only the header flag.");

                const int maximumLightId = 0xFFF;
                var occupiedIds = localRecords
                    .Where(r => r.FormKey.ID <= maximumLightId)
                    .Select(r => r.FormKey.ID)
                    .ToHashSet();
                var availableIds = Enumerable.Range(0x800, 0x800)
                    .Concat(Enumerable.Range(0, 0x800))
                    .Select(id => (uint)id)
                    .Where(id => !occupiedIds.Contains(id))
                    .Take(overflowRecords.Length)
                    .ToArray();
                if (availableIds.Length != overflowRecords.Length)
                    throw new InvalidOperationException($"The plugin has {localRecords.Length} local records and not enough free IDs in 0x000-0xFFF to compact safely.");

                var formKeyMap = new Dictionary<FormKey, FormKey>();
                for (var i = 0; i < overflowRecords.Length; i++)
                {
                    var newKey = FormKey.Factory($"{availableIds[i]:X6}:{mod.ModKey.FileName}");
                    formKeyMap.Add(overflowRecords[i].FormKey, newKey);
                }

                var expectedRecords = GetRecordSignatures(sourceRecords, formKeyMap);
                var expectedLinks = GetLinkSignatures(sourceRecords, formKeyMap);
                foreach (var record in sourceRecords)
                {
                    if (record is IFormLinkContainer linkContainer)
                        linkContainer.RemapLinks(formKeyMap);

                    if (formKeyMap.TryGetValue(record.FormKey, out var newKey))
                    {
                        var formKeyProperty = record.GetType().GetProperty(
                            nameof(IMajorRecord.FormKey),
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic);
                        var setter = formKeyProperty?.GetSetMethod(nonPublic: true);
                        if (setter == null)
                            throw new InvalidOperationException($"Cannot safely update FormID on record type {record.GetType().Name}.");
                        setter.Invoke(record, new object[] { newKey });
                    }
                }
                mod.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                mod.WriteToBinary(destinationPath);

                var compacted = SkyrimMod.CreateFromBinary(destinationPath, SkyrimRelease.SkyrimSE);
                var compactedRecords = compacted.EnumerateMajorRecords().ToArray();
                var compactedLocalRecords = compactedRecords.Where(r => r.FormKey.ModKey == compacted.ModKey).ToArray();
                if ((compacted.ModHeader.Flags & SkyrimModHeader.HeaderFlag.Small) == 0 ||
                    !expectedRecords.SequenceEqual(GetRecordSignatures(compactedRecords, null), StringComparer.Ordinal) ||
                    !expectedLinks.SequenceEqual(GetLinkSignatures(compactedRecords, null), StringComparer.Ordinal) ||
                    compactedRecords.Length != sourceRecords.Length ||
                    compactedLocalRecords.Any(r => r.FormKey.ID > maximumLightId))
                    throw new InvalidDataException("Output verification failed: ESL flag, record identities, internal links, or local FormID range did not match the staged mapping.");

                var result = new
                {
                    success = true,
                    source = sourcePath,
                    output = destinationPath,
                    isLight = true,
                    totalMajorRecords = compactedRecords.Length,
                    localRecords = compactedLocalRecords.Length,
                    localFormIdMax = compactedLocalRecords.Max(r => r.FormKey.ID),
                    formIdMap = formKeyMap.OrderBy(kv => kv.Key.ID).Select(kv => new { old = kv.Key.ToString(), @new = kv.Value.ToString() }).ToArray(),
                    masterFiles = compacted.ModHeader.MasterReferences.Select(m => m.Master.FileName.ToString()).ToArray(),
                    saveCompatibilityWarning = "Plugin-owned FormIDs changed. Existing saves, scripts, patches, and other files that store the old IDs may need updates."
                };
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                else
                {
                    Console.WriteLine($"Compacted and flagged {Path.GetFileName(destinationPath)} as ESL.");
                    Console.WriteLine($"Remapped {formKeyMap.Count} plugin-owned FormIDs; maximum local ID is 0x{compactedLocalRecords.Max(r => r.FormKey.ID):X}.");
                    Console.WriteLine("Warning: changed FormIDs may affect existing saves and dependent files. Review the mapping before deployment.");
                }
            }
            catch (Exception ex)
            {
                if (json)
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }, new JsonSerializerOptions { WriteIndented = true }));
                else
                    Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, pluginArg, outputOption, jsonOption);

        return cmd;
    }

    private static string[] GetRecordSignatures(
        IEnumerable<IMajorRecordGetter> records,
        IReadOnlyDictionary<FormKey, FormKey>? remap)
    {
        return records
            .Select(record =>
            {
                var key = remap != null && remap.TryGetValue(record.FormKey, out var updated) ? updated : record.FormKey;
                return $"{key}|{record.GetType().FullName}";
            })
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetLinkSignatures(
        IEnumerable<IMajorRecordGetter> records,
        IReadOnlyDictionary<FormKey, FormKey>? remap)
    {
        var signatures = new List<string>();
        foreach (var record in records)
        {
            var ownerKey = remap != null && remap.TryGetValue(record.FormKey, out var updatedOwner) ? updatedOwner : record.FormKey;
            if (record is not IFormLinkContainerGetter container)
                continue;

            foreach (var link in container.EnumerateFormLinks())
            {
                var targetKey = remap != null && remap.TryGetValue(link.FormKey, out var updatedTarget) ? updatedTarget : link.FormKey;
                signatures.Add($"{ownerKey}|{targetKey}");
            }
        }
        return signatures.OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
    }
}
