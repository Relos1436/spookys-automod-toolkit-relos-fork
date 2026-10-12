using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;

namespace SpookysAutomod.Esp.Builders;

/// <summary>
/// Fluent builder for creating a standalone interior Cell record (not attached to any
/// worldspace). Interior cells in Mutagen live under mod.Cells as a list of CellBlock, each
/// holding CellSubBlock entries, each holding the actual Cell records - mirroring Skyrim's own
/// GRUP nesting (Interior Cell Block -> Interior Cell Sub-Block -> CELL). A single block/sub-
/// block pair (both numbered 0) is enough for a small utility cell that only ever holds a
/// handful of placed references.
/// </summary>
public class CellBuilder
{
    private readonly SkyrimMod _mod;
    private readonly Cell _cell;

    public CellBuilder(SkyrimMod mod, string editorId)
    {
        _mod = mod;
        _cell = new Cell(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE);
        _cell.EditorID = editorId;

        var subBlock = new CellSubBlock { BlockNumber = 0 };
        subBlock.Cells.Add(_cell);

        var block = new CellBlock { BlockNumber = 0 };
        block.SubBlocks.Add(subBlock);

        mod.Cells.Add(block);
    }

    public CellBuilder WithName(string name)
    {
        _cell.Name = name;
        return this;
    }

    /// <summary>
    /// Marker call only - a cell with no worldspace, no map marker, and nothing placed but
    /// disabled references is already "hidden" by construction (nothing ever routes the player
    /// to it). Kept as a named step in the builder chain for clarity at call sites.
    /// </summary>
    public CellBuilder AsHiddenUtilityCell()
    {
        return this;
    }

    public Cell Build() => _cell;
}
