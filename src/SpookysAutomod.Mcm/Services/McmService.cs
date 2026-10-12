using System.Text.Json;
using SpookysAutomod.Core.Logging;
using SpookysAutomod.Core.Models;
using SpookysAutomod.Mcm.Builders;
using SpookysAutomod.Mcm.Models;

namespace SpookysAutomod.Mcm.Services;

/// <summary>
/// High-level service for MCM Helper configuration operations.
/// </summary>
public class McmService
{
    private readonly IModLogger _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public McmService(IModLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a new MCM configuration file.
    /// </summary>
    public Result<string> Create(string modName, string displayName, string outputPath)
    {
        try
        {
            var config = new McmBuilder(modName, displayName)
                .AddPage("General")
                .AddHeader("Settings")
                .AddText("Configure your mod settings below.")
                .AddEmpty()
                .Build();

            var json = JsonSerializer.Serialize(config, _jsonOptions);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, json);

            _logger.Info($"Created MCM config: {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Failed to create MCM config: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Load an existing MCM configuration.
    /// </summary>
    public Result<McmConfig> Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return Result<McmConfig>.Fail($"Config not found: {configPath}");
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<McmConfig>(json, _jsonOptions);

            if (config == null)
            {
                return Result<McmConfig>.Fail("Failed to parse MCM config");
            }

            return Result<McmConfig>.Ok(config);
        }
        catch (Exception ex)
        {
            return Result<McmConfig>.Fail(
                $"Failed to load MCM config: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Save an MCM configuration.
    /// </summary>
    public Result<string> Save(McmConfig config, string outputPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, json);

            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(
                $"Failed to save MCM config: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Add a toggle to an existing config.
    /// </summary>
    public Result<McmConfig> AddToggle(McmConfig config, string id, string text, string? help = null, string? page = null, bool defaultValue = false)
    {
        var targetPage = GetOrCreatePage(config, page);

        targetPage.Content.Add(new McmControl
        {
            Type = McmControlType.Toggle,
            Id = id,
            Text = text,
            Help = help,
            SourceType = "ModSettingBool",
            // valueOptions is where MCM Helper actually reads the binding from - a toggle with
            // no sourceType is inert.
            ValueOptions = new McmValueOptions
            {
                SourceType = "ModSettingBool",
                DefaultValue = defaultValue
            }
        });

        return Result<McmConfig>.Ok(config);
    }

    /// <summary>
    /// Add a slider to an existing config.
    /// </summary>
    public Result<McmConfig> AddSlider(McmConfig config, string id, string text, float min, float max, float step = 1, string? help = null, string? page = null, float? defaultValue = null, string? formatString = null)
    {
        var targetPage = GetOrCreatePage(config, page);

        // Default the display precision to match the step, so a 0.05 step doesn't render as a
        // whole number - "{N}" is a decimal-place count, not an argument index.
        formatString ??= step >= 1f ? "{0}" : step >= 0.1f ? "{1}" : "{2}";

        targetPage.Content.Add(new McmControl
        {
            Type = McmControlType.Slider,
            Id = id,
            Text = text,
            Help = help,
            Min = min,
            Max = max,
            Step = step,
            FormatString = formatString,
            SourceType = "ModSettingFloat",
            ValueOptions = new McmValueOptions
            {
                SourceType = "ModSettingFloat",
                Min = min,
                Max = max,
                Step = step,
                FormatString = formatString,
                DefaultValue = defaultValue ?? min
            }
        });

        return Result<McmConfig>.Ok(config);
    }

    /// <summary>
    /// Generate the settings.ini defaults file that MUST sit next to config.json in
    /// MCM/Config/&lt;modName&gt;/. This is NOT optional: without it MCM Helper registers none of
    /// the ModSetting* ids and drops the entire menu from the MCM list, logging nothing at all.
    /// A "defaultValue" inside config.json does not substitute for it.
    ///
    /// Setting ids are written as "name:Section", so they are grouped here into [Section]
    /// blocks. Ids with no section land in [General].
    /// </summary>
    public Result<string> GenerateSettingsIni(string configPath, string? outputPath = null)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return Result<string>.Fail($"MCM config not found: {configPath}");
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<McmConfig>(json, _jsonOptions);
            if (config == null)
            {
                return Result<string>.Fail($"Could not parse MCM config: {configPath}");
            }

            // Section -> (key -> default value), preserving first-seen order for readability.
            var sections = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);
            var count = 0;

            foreach (var page in config.Content)
            {
                foreach (var control in page.Content)
                {
                    var sourceType = control.ValueOptions?.SourceType ?? control.SourceType;
                    if (string.IsNullOrWhiteSpace(control.Id) ||
                        string.IsNullOrWhiteSpace(sourceType) ||
                        !sourceType.StartsWith("ModSetting", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;  // headers, text, and Papyrus-backed controls need no INI default
                    }

                    var parts = control.Id.Split(':', 2);
                    var key = parts[0].Trim();
                    var section = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
                        ? parts[1].Trim()
                        : "General";

                    var value = FormatDefaultValue(control.ValueOptions?.DefaultValue, sourceType, control.ValueOptions?.Min ?? control.Min);

                    if (!sections.TryGetValue(section, out var entries))
                    {
                        entries = new List<KeyValuePair<string, string>>();
                        sections[section] = entries;
                    }
                    if (entries.Any(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;  // duplicate id, keep the first
                    }
                    entries.Add(new KeyValuePair<string, string>(key, value));
                    count++;
                }
            }

            if (count == 0)
            {
                return Result<string>.Fail(
                    "No ModSetting* controls found in the config, so there is nothing to write.",
                    null,
                    new List<string>
                    {
                        "Give each interactive control a valueOptions.sourceType of ModSettingBool, ModSettingInt, or ModSettingFloat.",
                        "Controls bound to Papyrus properties instead do not belong in settings.ini."
                    });
            }

            outputPath ??= Path.Combine(Path.GetDirectoryName(configPath) ?? ".", "settings.ini");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"; Default values for the {config.ModName} MCM menu.");
            sb.AppendLine("; MCM Helper REQUIRES this file - without it none of the ModSetting entries in");
            sb.AppendLine("; config.json register and the whole menu silently disappears from the MCM list.");
            sb.AppendLine("; Do not edit this to change your settings: MCM Helper writes player choices to");
            sb.AppendLine($"; Data\\MCM\\Settings\\{config.ModName}.ini, which overrides everything here.");

            foreach (var (section, entries) in sections)
            {
                sb.AppendLine();
                sb.AppendLine($"[{section}]");
                foreach (var (key, value) in entries)
                {
                    sb.AppendLine($"{key}={value}");
                }
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, sb.ToString());

            _logger.Info($"Generated MCM settings.ini with {count} default(s): {outputPath}");
            return Result<string>.Ok(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Failed to generate settings.ini: {ex.Message}", ex.StackTrace);
        }
    }

    private static string FormatDefaultValue(object? defaultValue, string sourceType, float? min)
    {
        var isBool = sourceType.Equals("ModSettingBool", StringComparison.OrdinalIgnoreCase);

        if (defaultValue is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True: return "1";
                case JsonValueKind.False: return "0";
                case JsonValueKind.Number: return element.GetRawText();
                case JsonValueKind.String: return element.GetString() ?? (isBool ? "0" : "0");
            }
        }
        else if (defaultValue is bool b)
        {
            return b ? "1" : "0";
        }
        else if (defaultValue != null)
        {
            return Convert.ToString(defaultValue, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
        }

        // No explicit default: booleans fall back to off, numerics to their minimum.
        if (isBool) return "0";
        return (min ?? 0f).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Validate an MCM configuration file.
    /// </summary>
    public Result<ValidationResult> Validate(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return Result<ValidationResult>.Fail($"Config not found: {configPath}");
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<McmConfig>(json, _jsonOptions);

            if (config == null)
            {
                errors.Add("Failed to parse JSON");
                return Result<ValidationResult>.Ok(new ValidationResult
                {
                    IsValid = false,
                    Errors = errors,
                    Warnings = warnings
                });
            }

            // Validate required fields
            if (string.IsNullOrEmpty(config.ModName))
                errors.Add("Missing required field: modName");

            if (string.IsNullOrEmpty(config.DisplayName))
                errors.Add("Missing required field: displayName");

            if (config.Content.Count == 0)
                warnings.Add("Configuration has no pages");

            // Validate controls
            var controlIds = new HashSet<string>();
            foreach (var page in config.Content)
            {
                if (string.IsNullOrEmpty(page.PageDisplayName))
                    warnings.Add("Page has no display name");

                foreach (var control in page.Content)
                {
                    if (!string.IsNullOrEmpty(control.Id))
                    {
                        if (!controlIds.Add(control.Id))
                            errors.Add($"Duplicate control ID: {control.Id}");
                    }

                    // Validate slider ranges
                    if (control.Type == McmControlType.Slider)
                    {
                        if (control.Min == null || control.Max == null)
                            errors.Add($"Slider '{control.Id}' missing min/max values");
                        else if (control.Min >= control.Max)
                            errors.Add($"Slider '{control.Id}' has invalid range (min >= max)");
                    }

                    // Validate menu/enum options
                    if (control.Type == McmControlType.Menu || control.Type == McmControlType.Enum)
                    {
                        if (control.Options == null || control.Options.Count == 0)
                            errors.Add($"Menu/Enum '{control.Id}' has no options");
                    }
                }
            }

            return Result<ValidationResult>.Ok(new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            });
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return Result<ValidationResult>.Ok(new ValidationResult
            {
                IsValid = false,
                Errors = errors,
                Warnings = warnings
            });
        }
        catch (Exception ex)
        {
            return Result<ValidationResult>.Fail(
                $"Validation failed: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Get MCM config info.
    /// </summary>
    public Result<McmInfo> GetInfo(string configPath)
    {
        var loadResult = Load(configPath);
        if (!loadResult.Success)
            return Result<McmInfo>.Fail(loadResult.Error!);

        var config = loadResult.Value!;
        var controlCount = config.Content.Sum(p => p.Content.Count);

        return Result<McmInfo>.Ok(new McmInfo
        {
            ModName = config.ModName,
            DisplayName = config.DisplayName,
            MinMcmVersion = config.MinMcmVersion,
            PageCount = config.Content.Count,
            ControlCount = controlCount,
            Pages = config.Content.Select(p => new McmPageInfo
            {
                Name = p.PageDisplayName,
                ControlCount = p.Content.Count
            }).ToList()
        });
    }

    private static McmPage GetOrCreatePage(McmConfig config, string? pageName)
    {
        if (pageName != null)
        {
            var existing = config.Content.FirstOrDefault(p =>
                p.PageDisplayName.Equals(pageName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            var newPage = new McmPage { PageDisplayName = pageName };
            config.Content.Add(newPage);
            return newPage;
        }

        if (config.Content.Count == 0)
        {
            var defaultPage = new McmPage { PageDisplayName = "General" };
            config.Content.Add(defaultPage);
            return defaultPage;
        }

        return config.Content[^1];
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class McmInfo
{
    public string ModName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int? MinMcmVersion { get; set; }
    public int PageCount { get; set; }
    public int ControlCount { get; set; }
    public List<McmPageInfo> Pages { get; set; } = new();
}

public class McmPageInfo
{
    public string Name { get; set; } = "";
    public int ControlCount { get; set; }
}
