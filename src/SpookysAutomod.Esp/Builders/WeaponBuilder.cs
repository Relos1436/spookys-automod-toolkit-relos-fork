using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace SpookysAutomod.Esp.Builders;

/// <summary>
/// Fluent builder for creating Weapon records.
/// </summary>
public class WeaponBuilder
{
    private readonly SkyrimMod _mod;
    private readonly Weapon _weapon;

    public WeaponBuilder(SkyrimMod mod, string editorId)
    {
        _mod = mod;
        _weapon = mod.Weapons.AddNew();
        _weapon.EditorID = editorId;
        // Set defaults
        _weapon.BasicStats = new WeaponBasicStats
        {
            Damage = 10,
            Weight = 5,
            Value = 100
        };
        // A Creation-Kit-authored weapon always has real bounds computed from its mesh. Without
        // this, ObjectBounds defaults to a fully degenerate (0,0,0)-(0,0,0) box, which has been
        // confirmed (via a real crash log) to crash the game's inventory item-card 3D preview
        // code for at least one record. WithBounds() can override this with real dimensions;
        // this default just needs to be non-zero, not precisely accurate.
        _weapon.ObjectBounds = new ObjectBounds
        {
            First = new P3Int16(-5, -5, -5),
            Second = new P3Int16(5, 5, 5)
        };

        // A weapon built here (via AddNew(), never touched by the Creation Kit) leaves several
        // auxiliary reference fields completely null: EquipmentType, BlockBashImpact,
        // AlternateBlockMaterial, ImpactDataSet, the three sounds below, and DetectionSoundLevel.
        // A real CK-authored weapon always has these populated (CK fills them in from the chosen
        // weapon type/material preset). Confirmed via a real crash log + a direct field-by-field
        // diff against a working released mod's one-handed dagger-type weapon that a weapon with
        // several of these null can crash the game's inventory item-card code. These values are
        // copied from that verified-working one-handed dagger reference (all real Skyrim.esm
        // forms - no new master required) and applied as a universal baseline default: better to
        // have "close enough for a one-handed weapon" values than null for every other weapon
        // type too, pending someone verifying accurate per-type vanilla equivalents.
        _weapon.EquipmentType.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x13F42));
        _weapon.BlockBashImpact.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x183FF));
        _weapon.AlternateBlockMaterial.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x774C2));
        _weapon.ImpactDataSet.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x13CAC));
        _weapon.AttackFailSound.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x3C730));
        _weapon.EquipSound.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x3CED9));
        _weapon.UnequipSound.SetTo(new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x3CEDB));
        _weapon.DetectionSoundLevel = SoundLevel.Silent;
    }

    public WeaponBuilder WithBounds(short minX, short minY, short minZ, short maxX, short maxY, short maxZ)
    {
        _weapon.ObjectBounds = new ObjectBounds
        {
            First = new P3Int16(minX, minY, minZ),
            Second = new P3Int16(maxX, maxY, maxZ)
        };
        return this;
    }

    public WeaponBuilder WithName(string name)
    {
        _weapon.Name = name;
        return this;
    }

    public WeaponBuilder WithDescription(string description)
    {
        _weapon.Description = description;
        return this;
    }

    public WeaponBuilder WithDamage(ushort damage)
    {
        _weapon.BasicStats ??= new WeaponBasicStats();
        _weapon.BasicStats.Damage = damage;
        return this;
    }

    public WeaponBuilder WithWeight(float weight)
    {
        _weapon.BasicStats ??= new WeaponBasicStats();
        _weapon.BasicStats.Weight = weight;
        return this;
    }

    public WeaponBuilder WithValue(uint value)
    {
        _weapon.BasicStats ??= new WeaponBasicStats();
        _weapon.BasicStats.Value = value;
        return this;
    }

    public WeaponBuilder WithSpeed(float speed)
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.Speed = speed;
        return this;
    }

    public WeaponBuilder WithReach(float reach)
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.Reach = reach;
        return this;
    }

    public WeaponBuilder WithCriticalDamage(ushort criticalDamage)
    {
        _weapon.Critical ??= new CriticalData();
        _weapon.Critical.Damage = criticalDamage;
        return this;
    }

    public WeaponBuilder WithKeyword(FormKey keywordFormKey)
    {
        _weapon.Keywords ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>();
        _weapon.Keywords.Add(keywordFormKey.ToLink<IKeywordGetter>());
        return this;
    }

    public WeaponBuilder AsSword()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.OneHandSword;
        return this;
    }

    public WeaponBuilder AsGreatsword()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.TwoHandSword;
        return this;
    }

    public WeaponBuilder AsDagger()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.OneHandDagger;
        return this;
    }

    public WeaponBuilder AsWarAxe()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.OneHandAxe;
        return this;
    }

    public WeaponBuilder AsBattleaxe()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.TwoHandAxe;
        return this;
    }

    public WeaponBuilder AsMace()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.OneHandMace;
        return this;
    }

    public WeaponBuilder AsWarhammer()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.TwoHandAxe;
        return this;
    }

    public WeaponBuilder AsBow()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.Bow;
        return this;
    }

    public WeaponBuilder AsStaff()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.Staff;
        return this;
    }

    public WeaponBuilder AsCrossbow()
    {
        _weapon.Data ??= new WeaponData();
        _weapon.Data.AnimationType = WeaponAnimationType.Crossbow;
        return this;
    }

    /// <summary>
    /// Sets the weapon's 3D model path.
    /// Use paths relative to Data/Meshes/, e.g., "Weapons\Iron\IronSword.nif"
    /// </summary>
    /// <remarks>
    /// Also auto-creates a matching FirstPersonModel Static wrapping the same nif, since a
    /// completely null FirstPersonModel (never set by AddNew()) has been confirmed via a real
    /// crash log to crash the game when the weapon is equipped/viewed in the inventory. Call
    /// WithFirstPersonModel() afterward to point at a different mesh (or an existing Static)
    /// instead of this auto-created one.
    /// </remarks>
    public WeaponBuilder WithModel(string modelPath)
    {
        _weapon.Model = new Model { File = modelPath };

        var firstPersonStatic = _mod.Statics.AddNew();
        firstPersonStatic.EditorID = $"{_weapon.EditorID}_1stPerson";
        firstPersonStatic.Model = new Model { File = modelPath };
        _weapon.FirstPersonModel.SetTo(firstPersonStatic.FormKey);

        return this;
    }

    /// <summary>
    /// Overrides the weapon's first-person model - either a different mesh (creates a new
    /// Static wrapping it) or an existing Static record's FormKey (e.g. a vanilla one, to avoid
    /// creating a duplicate). Only needed if the auto-created one from WithModel() isn't right.
    /// </summary>
    public WeaponBuilder WithFirstPersonModel(string modelPath)
    {
        var firstPersonStatic = _mod.Statics.AddNew();
        firstPersonStatic.EditorID = $"{_weapon.EditorID}_1stPerson";
        firstPersonStatic.Model = new Model { File = modelPath };
        _weapon.FirstPersonModel.SetTo(firstPersonStatic.FormKey);
        return this;
    }

    public WeaponBuilder WithFirstPersonModel(FormKey existingStaticFormKey)
    {
        _weapon.FirstPersonModel.SetTo(existingStaticFormKey);
        return this;
    }

    /// <summary>
    /// Uses a vanilla iron sword model - good for testing.
    /// </summary>
    public WeaponBuilder WithIronSwordModel()
    {
        return WithModel(@"Weapons\Iron\IronSword.nif");
    }

    /// <summary>
    /// Uses a vanilla steel sword model.
    /// </summary>
    public WeaponBuilder WithSteelSwordModel()
    {
        return WithModel(@"Weapons\Steel\SteelSword.nif");
    }

    /// <summary>
    /// Uses a vanilla iron dagger model.
    /// </summary>
    public WeaponBuilder WithIronDaggerModel()
    {
        return WithModel(@"Weapons\Iron\IronDagger.nif");
    }

    /// <summary>
    /// Uses a vanilla hunting bow model.
    /// </summary>
    public WeaponBuilder WithHuntingBowModel()
    {
        return WithModel(@"Weapons\IronBow.nif");
    }

    public Weapon Build() => _weapon;
}
