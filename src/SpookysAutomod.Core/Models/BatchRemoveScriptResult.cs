namespace SpookysAutomod.Core.Models;

/// <summary>
/// Result from a batch script-removal operation
/// </summary>
public class BatchRemoveScriptResult
{
    /// <summary>
    /// Number of records successfully modified
    /// </summary>
    public int RecordsModified { get; set; }

    /// <summary>
    /// Total number of individual script entries removed across all modified records
    /// </summary>
    public int ScriptsRemoved { get; set; }

    /// <summary>
    /// List of modified record EditorIDs
    /// </summary>
    public List<string> ModifiedRecords { get; set; } = new();

    /// <summary>
    /// Records that matched the filter but were left untouched, with the reason
    /// </summary>
    public List<string> SkippedRecords { get; set; } = new();

    /// <summary>
    /// Path to the created patch file
    /// </summary>
    public string PatchPath { get; set; } = string.Empty;
}
