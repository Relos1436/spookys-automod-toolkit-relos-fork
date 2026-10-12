using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace SpookysAutomod.Esp.Builders;

/// <summary>
/// Fluent builder for creating Container (CONT) records.
/// </summary>
public class ContainerBuilder
{
    private readonly SkyrimMod _mod;
    private readonly Container _container;

    public ContainerBuilder(SkyrimMod mod, string editorId)
    {
        _mod = mod;
        _container = new Container(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE);
        _container.EditorID = editorId;
        mod.Containers.Add(_container);
    }

    /// <summary>
    /// Seeds the container with starting stock. Adds the item's owning plugin as a master if
    /// it isn't already (same EnsureMaster pattern as RecipeBuilder).
    /// </summary>
    public ContainerBuilder WithItem(FormKey item, int count = 1)
    {
        _container.Items ??= new ExtendedList<ContainerEntry>();
        _container.Items.Add(new ContainerEntry
        {
            Item = new ContainerItem
            {
                Item = item.ToLink<Mutagen.Bethesda.Skyrim.IItemGetter>(),
                Count = count,
            },
        });

        if (item.ModKey != _mod.ModKey &&
            !_mod.ModHeader.MasterReferences.Any(m => m.Master == item.ModKey))
        {
            _mod.ModHeader.MasterReferences.Add(new MasterReference { Master = item.ModKey });
        }

        return this;
    }

    public ContainerBuilder WithName(string name)
    {
        _container.Name = name;
        return this;
    }

    /// <summary>
    /// Sets the container's carry weight limit (0 = unlimited).
    /// </summary>
    public ContainerBuilder WithWeight(float weight)
    {
        _container.Weight = weight;
        return this;
    }

    /// <summary>
    /// Marks the container as respawning (not wanted for a persistent storage backend -
    /// respawn would reset/clear contents). Off by default; call only if actually desired.
    /// </summary>
    public ContainerBuilder Respawns()
    {
        _container.Flags |= Container.Flag.Respawns;
        return this;
    }

    public Container Build() => _container;
}
