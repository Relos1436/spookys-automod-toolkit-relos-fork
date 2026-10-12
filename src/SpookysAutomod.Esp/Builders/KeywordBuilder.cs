using Mutagen.Bethesda.Skyrim;

namespace SpookysAutomod.Esp.Builders;

/// <summary>
/// Fluent builder for creating Keyword (KYWD) records.
///
/// Keywords are deliberately minimal records - an EditorID is essentially all they carry that
/// matters. Their value is as a stable tag other systems can match on: SkyPatcher can attach a
/// keyword defined here onto any record at runtime, letting a plugin classify items it does not
/// own without editing or overriding them.
///
/// Note: unlike most groups in Mutagen, SkyrimGroup&lt;Keyword&gt; exposes no AddNew() - the record
/// is constructed against the mod (which assigns it a FormKey) and then added to the group.
/// </summary>
public class KeywordBuilder
{
    private readonly Keyword _keyword;

    public KeywordBuilder(SkyrimMod mod, string editorId)
    {
        _keyword = new Keyword(mod)
        {
            EditorID = editorId
        };
        mod.Keywords.Add(_keyword);
    }

    /// <summary>
    /// Sets the keyword's display colour. Purely cosmetic - only surfaces in the Creation Kit.
    /// </summary>
    public KeywordBuilder WithColor(System.Drawing.Color color)
    {
        _keyword.Color = color;
        return this;
    }

    public Keyword Build() => _keyword;
}
