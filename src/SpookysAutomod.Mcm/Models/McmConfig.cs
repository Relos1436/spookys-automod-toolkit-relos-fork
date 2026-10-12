using System.Text.Json.Serialization;

namespace SpookysAutomod.Mcm.Models;

/// <summary>
/// MCM Helper configuration file model.
/// Based on SkyUI MCM Helper format.
/// </summary>
public class McmConfig
{
    [JsonPropertyName("modName")]
    public string ModName { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Omitted entirely when null. Setting this HIGHER than the installed MCM Helper supports
    /// makes the menu silently vanish, so leaving it unset is the safe default - known-good
    /// real-world configs (e.g. FasterLoadscreens) omit it.
    /// </summary>
    [JsonPropertyName("minMcmVersion")]
    public int? MinMcmVersion { get; set; }

    /// <summary>
    /// MUST be emitted, even empty. MCM Helper contains the check
    /// 'modName: "{}" did not match plugin' - with pluginRequirements absent it falls back to
    /// resolving modName against the load order, and a failure there drops the whole config
    /// with nothing logged. An explicit empty list satisfies the check unconditionally.
    /// </summary>
    [JsonPropertyName("pluginRequirements")]
    public List<string> PluginRequirements { get; set; } = new();

    /// <summary>
    /// Serialized as "pages" - MCM Helper does NOT recognise a top-level "content" array.
    /// </summary>
    [JsonPropertyName("pages")]
    public List<McmPage> Content { get; set; } = new();
}

public class McmPage
{
    [JsonPropertyName("pageDisplayName")]
    public string PageDisplayName { get; set; } = "";

    [JsonPropertyName("content")]
    public List<McmControl> Content { get; set; } = new();
}

public class McmControl
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("help")]
    public string? Help { get; set; }

    // Toggle specific
    [JsonPropertyName("groupControl")]
    public int? GroupControl { get; set; }

    // Slider specific. NOT serialized at control level - MCM Helper reads these from
    // valueOptions, so they are mirrored into ValueOptions when the control is built. They
    // remain here purely as a convenient in-memory view for validate/info.
    [JsonIgnore]
    public float? Min { get; set; }

    [JsonIgnore]
    public float? Max { get; set; }

    [JsonIgnore]
    public float? Step { get; set; }

    [JsonIgnore]
    public string? FormatString { get; set; }

    // Menu/Enum specific - likewise belongs in valueOptions, not at control level.
    [JsonIgnore]
    public List<string>? Options { get; set; }

    [JsonPropertyName("shortNames")]
    public List<string>? ShortNames { get; set; }

    // Input specific
    [JsonPropertyName("valueOptions")]
    public McmValueOptions? ValueOptions { get; set; }

    // Source binding - belongs inside valueOptions, mirrored there when the control is built.
    [JsonIgnore]
    public string? SourceType { get; set; }

    [JsonPropertyName("sourceForm")]
    public string? SourceForm { get; set; }

    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; set; }

    [JsonPropertyName("scriptName")]
    public string? ScriptName { get; set; }

    // Color specific
    [JsonPropertyName("defaultColor")]
    public string? DefaultColor { get; set; }

    // Keymap specific
    [JsonPropertyName("ignoreConflicts")]
    public bool? IgnoreConflicts { get; set; }
}

public class McmValueOptions
{
    [JsonPropertyName("min")]
    public float? Min { get; set; }

    [JsonPropertyName("max")]
    public float? Max { get; set; }

    [JsonPropertyName("step")]
    public float? Step { get; set; }

    /// <summary>
    /// Slider display format. The digit is the number of decimal places, and surrounding text
    /// is kept verbatim - e.g. "{2}" renders 0.33, "{0}%" renders 50%, "{1} Days" renders 3.0 Days.
    /// </summary>
    [JsonPropertyName("formatString")]
    public string? FormatString { get; set; }

    /// <summary>
    /// ModSettingBool / ModSettingInt / ModSettingFloat bind the control to an INI-backed
    /// setting, requiring no Papyrus script of your own. Every such id ALSO needs a default in
    /// MCM/Config/&lt;modName&gt;/settings.ini or nothing registers (see McmService.GenerateSettingsIni).
    /// </summary>
    [JsonPropertyName("sourceType")]
    public string? SourceType { get; set; }

    [JsonPropertyName("sourceForm")]
    public string? SourceForm { get; set; }

    /// <summary>
    /// Bool for toggles, number for sliders/enums. Serialized as-is.
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    /// <summary>Enum/menu choices.</summary>
    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }
}

/// <summary>
/// MCM control types.
/// </summary>
public static class McmControlType
{
    public const string Header = "header";
    public const string Text = "text";
    public const string Toggle = "toggle";
    public const string Slider = "slider";
    public const string Menu = "menu";
    public const string Enum = "enum";
    public const string Color = "color";
    public const string Keymap = "keymap";
    public const string Input = "input";
    public const string HiddenToggle = "hiddenToggle";
    public const string Empty = "empty";
}
