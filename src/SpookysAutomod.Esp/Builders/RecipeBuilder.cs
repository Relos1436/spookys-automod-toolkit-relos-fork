using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace SpookysAutomod.Esp.Builders;

/// <summary>
/// Fluent builder for creating ConstructibleObject (crafting recipe) records.
/// </summary>
public class RecipeBuilder
{
    private readonly SkyrimMod _mod;
    private readonly ConstructibleObject _recipe;

    public RecipeBuilder(SkyrimMod mod, string editorId)
    {
        _mod = mod;
        _recipe = mod.ConstructibleObjects.AddNew();
        _recipe.EditorID = editorId;
        _recipe.CreatedObjectCount = 1;
        _recipe.Items = new ExtendedList<ContainerEntry>();
    }

    /// <summary>
    /// The item this recipe creates. Adds the owning plugin as a master if it isn't already.
    /// </summary>
    public RecipeBuilder Creates(FormKey createdObject, ushort count = 1)
    {
        _recipe.CreatedObject.SetTo(createdObject);
        _recipe.CreatedObjectCount = count;
        EnsureMaster(createdObject);
        return this;
    }

    /// <summary>
    /// The crafting station keyword (e.g. CraftingTanningRack, CraftingSmithingForge). Adds the
    /// owning plugin as a master if it isn't already.
    /// </summary>
    public RecipeBuilder AtWorkbench(FormKey workbenchKeyword)
    {
        _recipe.WorkbenchKeyword.SetTo(workbenchKeyword);
        EnsureMaster(workbenchKeyword);
        return this;
    }

    /// <summary>
    /// Adds a required ingredient. Adds the owning plugin as a master if it isn't already.
    /// </summary>
    public RecipeBuilder RequiresItem(FormKey item, int count = 1)
    {
        _recipe.Items ??= new ExtendedList<ContainerEntry>();
        _recipe.Items.Add(new ContainerEntry
        {
            Item = new ContainerItem
            {
                Item = item.ToLink<Mutagen.Bethesda.Skyrim.IItemGetter>(),
                Count = count,
            },
        });
        EnsureMaster(item);
        return this;
    }

    private void EnsureMaster(FormKey formKey)
    {
        if (formKey.ModKey == _mod.ModKey)
        {
            return;  // record being defined in this same plugin, not an external master
        }
        if (!_mod.ModHeader.MasterReferences.Any(m => m.Master == formKey.ModKey))
        {
            _mod.ModHeader.MasterReferences.Add(new MasterReference { Master = formKey.ModKey });
        }
    }

    public ConstructibleObject Build() => _recipe;
}
