using System.Reflection;
using System.Text.Json;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Strings;
using SpookysAutomod.Core.Models;

namespace SpookysAutomod.Esp.Services;

/// <summary>Explicit Fallout 4 operations. Sources are always read-only; outputs must be new files.</summary>
public sealed class Fallout4PluginService
{
    public Result<string> CreateRecord(string output, string recordType, string editorId, string? fieldsJson = null, bool light = false) => Run(() =>
    {
        var mod = new Fallout4Mod(ModKey.FromFileName(Path.GetFileName(output)), Fallout4Release.Fallout4);
        var type = typeof(Fallout4Mod).Assembly.GetType("Mutagen.Bethesda.Fallout4." + recordType)
            ?? throw new ArgumentException($"Unknown Fallout 4 record class: {recordType}");
        var group = ((IMod)mod).TryGetTopLevelGroup(type)
            ?? throw new ArgumentException("Only top-level record classes can be created.");
        var record = group.AddNew();
        record.EditorID = editorId;
        ApplyFields(record, fieldsJson);
        if (light) mod.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.Small;
        return WriteNew(mod, output);
    });

    private static Result<T> Run<T>(Func<T> action)
    {
        try { return Result<T>.Ok(action()); }
        catch (Exception ex) { return Result<T>.Fail(ex.GetBaseException().Message); }
    }

    public Result<string> Create(string output, bool light = false, string? author = null) => Run(() =>
    {
        var mod = new Fallout4Mod(ModKey.FromFileName(Path.GetFileName(output)), Fallout4Release.Fallout4);
        mod.ModHeader.Author = author;
        if (light) mod.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.Small;
        return WriteNew(mod, output);
    });

    public Result<object> Info(string plugin) => Run<object>(() =>
    {
        using var mod = Fallout4Mod.CreateFromBinaryOverlay(plugin, Fallout4Release.Fallout4);
        var records = mod.EnumerateMajorRecords().ToArray();
        return new { Game = "Fallout4", Plugin = mod.ModKey.ToString(), mod.ModHeader.Author,
            mod.ModHeader.Description, Flags = mod.ModHeader.Flags.ToString(),
            Masters = mod.ModHeader.MasterReferences.Select(m => m.Master.ToString()).ToArray(),
            RecordCount = records.Length,
            RecordTypes = records.GroupBy(r => r.GetType().Name.Replace("BinaryOverlay", ""))
                .ToDictionary(g => g.Key, g => g.Count()) };
    });

    public Result<object> ListRecords(string plugin, string? search = null, string? type = null) => Run<object>(() =>
    {
        using var mod = Fallout4Mod.CreateFromBinaryOverlay(plugin, Fallout4Release.Fallout4);
        return mod.EnumerateMajorRecords()
            .Where(r => type == null || r.GetType().Name.Replace("BinaryOverlay", "").Equals(type, StringComparison.OrdinalIgnoreCase))
            .Where(r => search == null || (r.EditorID?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || r.FormKey.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(r => new { FormKey = r.FormKey.ToString(), r.EditorID,
                RecordType = r.GetType().Name.Replace("BinaryOverlay", "") }).ToArray();
    });

    public Result<object> ViewRecord(string plugin, string record) => Run<object>(() =>
    {
        using var mod = Fallout4Mod.CreateFromBinaryOverlay(plugin, Fallout4Release.Fallout4);
        return Project(Find(mod, record), 0)!;
    });

    public Result<string> CreateOverride(string plugin, string record, string output,
        string? changesJson = null, bool light = false) => Run(() =>
    {
        using var source = Fallout4Mod.CreateFromBinaryOverlay(plugin, Fallout4Release.Fallout4);
        var original = Find(source, record);
        var patch = new Fallout4Mod(ModKey.FromFileName(Path.GetFileName(output)), Fallout4Release.Fallout4);
        if (patch.ModKey == source.ModKey) throw new ArgumentException("Patch filename must differ from the source plugin filename.");
        var copy = (IMajorRecord)original.DeepCopy();
        ApplyFields(copy, changesJson);
        // Nested records require parent contexts; reject instead of emitting a structurally invalid patch.
        var group = ((IMod)patch).TryGetTopLevelGroup(copy.GetType())
            ?? throw new NotSupportedException("This record is nested (for example CELL/REFR/INFO). Parent-context patching is not supported yet.");
        group.SetUntyped(copy);
        if (light) patch.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.Small;
        return WriteNew(patch, output);
    });

    private static void ApplyFields(object record, string? fieldsJson)
    {
        if (fieldsJson == null) return;
        using var fields = JsonDocument.Parse(fieldsJson);
        if (fields.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Changes must be a JSON object of field paths and values.");
        foreach (var field in fields.RootElement.EnumerateObject()) SetField(record, field.Name, field.Value);
    }

    private static IMajorRecordGetter Find(IFallout4ModGetter mod, string selector)
    {
        var matches = mod.EnumerateMajorRecords().Where(r =>
            r.FormKey.ToString().Equals(selector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.EditorID, selector, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length switch { 1 => matches[0], 0 => throw new ArgumentException($"Record not found: {selector}"),
            _ => throw new ArgumentException("Ambiguous EditorID; use the full FormKey instead.") };
    }

    private static void SetField(object record, string path, JsonElement value)
    {
        var parts = path.Split('.');
        if (parts.Any(p => p is "FormKey" or "FormVersion" or "VersionControl" or "MajorRecordFlagsRaw"))
            throw new ArgumentException($"Structural field cannot be edited: {path}");
        object target = record;
        foreach (var part in parts[..^1])
        {
            var p = target.GetType().GetProperty(part) ?? throw new ArgumentException($"Unknown field: {path}");
            if (p.PropertyType.IsValueType) throw new ArgumentException($"Cannot traverse a value-type field: {path}");
            target = p.GetValue(target) ?? throw new ArgumentException($"Null parent field: {path}");
        }
        var property = target.GetType().GetProperty(parts[^1]) ?? throw new ArgumentException($"Unknown field: {path}");
        if (!property.CanWrite) throw new ArgumentException($"Read-only field: {path}");
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        object? converted;
        if (typeof(ITranslatedStringGetter).IsAssignableFrom(type))
        {
            if (property.GetValue(target) is ITranslatedString existing) { existing.String = value.GetString(); return; }
            converted = new TranslatedString(Language.English, value.GetString());
        }
        else if (type == typeof(string)) converted = value.GetString();
        else if (type.IsEnum) converted = Enum.Parse(type, value.GetString()!, false);
        else if (type.IsPrimitive || type == typeof(decimal)) converted = JsonSerializer.Deserialize(value.GetRawText(), type);
        else throw new ArgumentException($"Field {path} is not a scalar. Editing links and collections is not supported yet.");
        property.SetValue(target, converted);
    }

    private static object? Project(object? value, int depth)
    {
        if (value == null) return null;
        var type = value.GetType();
        if (type.IsPrimitive || value is string or decimal) return value;
        if (type.IsEnum || value is FormKey or ModKey or Type) return value.ToString();
        if (value is ITranslatedStringGetter translated) return translated.String;
        if (depth >= 6) return value.ToString();
        if (value is System.Collections.IEnumerable list)
            return list.Cast<object?>().Select(v => Project(v, depth + 1)).ToArray();
        var result = new Dictionary<string, object?>();
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.Name != "Registration"))
            result[p.Name] = Project(p.GetValue(value), depth + 1);
        return result;
    }

    private static string WriteNew(Fallout4Mod mod, string output)
    {
        var path = Path.GetFullPath(output);
        if (File.Exists(path)) throw new IOException($"Output already exists; choose a new path: {path}");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryDirectory = Path.Combine(Path.GetDirectoryName(path)!, $".spooky-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var temporary = Path.Combine(temporaryDirectory, Path.GetFileName(path));
        try
        {
            mod.WriteToBinary(temporary);
            using (var check = Fallout4Mod.CreateFromBinaryOverlay(new ModPath(mod.ModKey, temporary), Fallout4Release.Fallout4))
                _ = check.EnumerateMajorRecords().Count();
            File.Move(temporary, path, false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (!Directory.EnumerateFileSystemEntries(temporaryDirectory).Any()) Directory.Delete(temporaryDirectory);
        }
        return path;
    }
}
