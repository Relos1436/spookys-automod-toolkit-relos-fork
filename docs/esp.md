# ESP Module Reference

The ESP module handles creation and editing of Skyrim plugin files (.esp/.esl).

## Commands

### create

Create a new ESP/ESL plugin file.

```bash
esp create <name> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `name` | Plugin filename (e.g., "MyMod.esp") |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--output`, `-o` | `.` | Output directory |
| `--light` | false | Create as ESL-flagged light plugin |
| `--author` | - | Author name in plugin header |
| `--description` | - | Description in plugin header |

**Examples:**
```bash
# Basic plugin
esp create "MyMod.esp"

# Light plugin with metadata
esp create "MyMod.esp" --light --author "YourName" --description "My awesome mod"

# Specify output directory
esp create "MyMod.esp" --output "./dist"
```

---

### info

Get information about an existing plugin.

```bash
esp info <plugin>
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Output includes:**
- Filename and path
- File size
- Light/Master flags
- Author (if set)
- Master file dependencies
- Record counts by type

**Example:**
```bash
esp info "MyMod.esp"
```

---

### flag-light

Set the ESL flag on an existing .esp without rewriting its records.

    esp flag-light <plugin> --output <staging-path-with-same-filename>

This command checks plugin-owned local FormIDs and refuses any ID above 0xFFF. Use it only when the plugin already fits the ESL range. It preserves the source and verifies the staged output.

### compact-light

Remap plugin-owned records into the ESL local FormID range, remap internal links, and set the ESL flag.

    esp compact-light <plugin> --output <staging-path-with-same-filename>

The output must use the source filename, go to a different path, and not already exist. The source is never overwritten. The command reports the old-to-new FormID map and reopens the output to verify the ESL flag, record identities, internal links, and local ID range.

**Compatibility warning:** compaction changes plugin-owned FormIDs. Existing saves, Papyrus scripts, patches, and other files that store those IDs may need updates. Inspect the map and dependent files before deployment. If no owned IDs exceed 0xFFF, compaction is unnecessary; use flag-light if only the header flag is needed.

---

### add-quest

Add a quest record to a plugin.

```bash
esp add-quest <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the quest |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--start-enabled` | false | Quest starts when game loads |
| `--run-once` | false | Quest runs only once |
| `--priority` | 50 | Quest priority (0-255) |

**Examples:**
```bash
# Basic quest
esp add-quest "MyMod.esp" "MyMod_MainQuest" --name "The Main Quest"

# Start-enabled quest (requires SEQ file)
esp add-quest "MyMod.esp" "MyMod_InitQuest" --name "Init Quest" --start-enabled --run-once
```

---

### add-spell

Add a spell record to a plugin.

```bash
esp add-spell <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the spell |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--type` | `spell` | Type: `spell`, `power`, `lesser-power`, `ability` |
| `--cost` | 0 | Base magicka cost |
| `--effect` | - | Effect preset (see below) |
| `--magnitude` | 25 | Effect magnitude |
| `--duration` | 0 | Effect duration in seconds (0 = instant) |

**Effect Presets:**
| Preset | Description |
|--------|-------------|
| `damage-health` | Deal damage to target's health |
| `restore-health` | Restore target's health |
| `damage-magicka` | Drain target's magicka |
| `restore-magicka` | Restore target's magicka |
| `damage-stamina` | Drain target's stamina |
| `restore-stamina` | Restore target's stamina |
| `fortify-health` | Temporarily increase max health |
| `fortify-magicka` | Temporarily increase max magicka |
| `fortify-stamina` | Temporarily increase max stamina |
| `fortify-armor` | Increase damage resistance |
| `fortify-attack` | Increase attack damage |

**Examples:**
```bash
# Damage spell - deals 50 fire damage
esp add-spell "MyMod.esp" "MyMod_Fireball" --name "Fireball" --effect damage-health --magnitude 50 --cost 45

# Healing spell - restores 30 health
esp add-spell "MyMod.esp" "MyMod_Heal" --name "Minor Heal" --effect restore-health --magnitude 30 --cost 25

# Buff spell - +50 health for 60 seconds
esp add-spell "MyMod.esp" "MyMod_Fortify" --name "Fortify Health" --effect fortify-health --magnitude 50 --duration 60 --cost 80

# Power (daily ability) - restore all magicka
esp add-spell "MyMod.esp" "MyMod_RacialPower" --name "Ancient Power" --type power --effect restore-magicka --magnitude 500

# Ability (passive) - constant 25% armor bonus
esp add-spell "MyMod.esp" "MyMod_Passive" --name "Tough Skin" --type ability --effect fortify-armor --magnitude 25
```

**Note:** Without `--effect`, the spell will exist but do nothing. Always specify an effect.

---

### add-global

Add a global variable to a plugin.

```bash
esp add-global <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the global |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--type` | `float` | Type: `short`, `long`, `float` |
| `--value` | 0 | Initial value |

**Examples:**
```bash
# Float global (for multipliers, percentages)
esp add-global "MyMod.esp" "MyMod_DamageMultiplier" --type float --value 1.5

# Integer global (for counts, flags)
esp add-global "MyMod.esp" "MyMod_Enabled" --type long --value 1

# Short global (for small values)
esp add-global "MyMod.esp" "MyMod_Counter" --type short --value 0
```

---

### add-weapon

Add a weapon record to a plugin.

```bash
esp add-weapon <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the weapon |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--description` | - | Item card description text. See note below - some UI mods crash without this set. |
| `--type` | `sword` | Type (see below) |
| `--damage` | 10 | Base damage |
| `--value` | 100 | Gold value |
| `--weight` | 5 | Weight |
| `--model` | - | Model path or preset |
| `--speed` | - | Attack speed multiplier (vanilla weapons are typically 0.8-1.3) |
| `--reach` | - | Attack reach multiplier (vanilla weapons are typically 0.7-1.5) |
| `--critical-damage` | - | Bonus damage applied on a critical hit |
| `--keyword` | - | Add a keyword, format `FormId:ModName.esp` (no `0x` prefix) or EditorID (can use multiple times) |
| `--bounds` | small non-zero default | Object bounds as `minX,minY,minZ,maxX,maxY,maxZ` (game units). A small non-zero default is applied automatically - only set this if you know the real mesh dimensions. |

**Weapon Types:**
- `sword` - One-handed sword
- `greatsword` - Two-handed sword
- `dagger` - Dagger
- `waraxe` - One-handed axe
- `battleaxe` - Two-handed axe
- `mace` - One-handed mace
- `warhammer` - Two-handed hammer
- `bow` - Bow
- `crossbow` - Crossbow
- `staff` - Staff

**Model Presets:**
- `iron-sword` - Iron sword model
- `steel-sword` - Steel sword model
- `iron-dagger` - Iron dagger model
- `hunting-bow` - Hunting bow model

**Important:** Without `--model`, the weapon will be invisible in-game.

**Important:** Always set `--description` for any weapon the player will see in their inventory
(not a purely internal/never-picked-up marker). At least one installed UI mod
(DescriptionFramework) has been confirmed to crash to desktop while building the item card for
a weapon with no description text set - the crash happened while a custom weapon with an empty
description was open in the inventory menu, traced via a real crash log back to
`ItemCardFixer::applyDescription`. A one-line flavor description is enough to avoid it.

**Equip-safety defaults:** a weapon record created here (via Mutagen's `AddNew()`, never touched
by the Creation Kit) is automatically given non-null `EquipmentType`, `BlockBashImpact`,
`AlternateBlockMaterial`, `ImpactDataSet`, `AttackFailSound`, `EquipSound`, `UnequipSound`,
`DetectionSoundLevel`, and (once `--model` is set) a matching `FirstPersonModel`. This was added
after a real, reproducible crash: a weapon with a null `FirstPersonModel` crashed the game the
moment it was equipped and viewed in the inventory. The defaults are copied from a verified
working one-handed dagger-type weapon - a reasonable baseline for any weapon type, not
necessarily precise for e.g. a bow or staff. Use `WithFirstPersonModel()` (builder) to point at
a different mesh or an existing Static if the auto-created one isn't right.

**Examples:**
```bash
# Sword with vanilla iron sword model
esp add-weapon "MyMod.esp" "MyMod_Sword" --name "Blade of Testing" --type sword --damage 25 --model iron-sword

# Custom model path
esp add-weapon "MyMod.esp" "MyMod_DaedricBlade" --name "Daedric Blade" --type sword --damage 50 --model "Weapons\Daedric\DaedricSword.nif"

# Bow
esp add-weapon "MyMod.esp" "MyMod_Bow" --name "Hunter's Bow" --type bow --damage 15 --model hunting-bow

# Non-combat prop weapon (e.g. a physical VR tool) matching an existing vanilla weapon's feel,
# with real WeapTypeDagger/WeapMaterialIron classification keywords
esp add-weapon "MyMod.esp" "MyMod_Tool" --name "My Tool" --type dagger --damage 0 --value 10 \
  --weight 0 --model "meshes\myTool.nif" --speed 1.3 --reach 0.7 --critical-damage 2 \
  --keyword "01E713:Skyrim.esm" --keyword "01E718:Skyrim.esm"
```

---

### add-armor

Add an armor record to a plugin.

```bash
esp add-armor <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the armor |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--type` | `light` | Type: `light`, `heavy`, `clothing` |
| `--slot` | `body` | Slot: `head`, `body`, `hands`, `feet`, `shield` |
| `--rating` | 10 | Armor rating |
| `--value` | 100 | Gold value |
| `--model` | - | Model path or preset |

**Model Presets:**
- `iron-cuirass` - Iron cuirass model
- `iron-helmet` - Iron helmet model
- `iron-gauntlets` - Iron gauntlets model
- `iron-boots` - Iron boots model
- `iron-shield` - Iron shield model

**Important:** Without `--model`, the armor will be invisible in-game.

**Examples:**
```bash
# Heavy armor cuirass
esp add-armor "MyMod.esp" "MyMod_Cuirass" --name "Steel Plate" --type heavy --slot body --rating 40 --model iron-cuirass

# Light armor helmet
esp add-armor "MyMod.esp" "MyMod_Hood" --name "Thief's Hood" --type light --slot head --rating 10 --model iron-helmet
```

---

### add-npc

Add an NPC record to a plugin.

```bash
esp add-npc <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the NPC |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--level` | 1 | NPC level |
| `--female` | false | NPC is female |
| `--essential` | false | NPC cannot be killed |
| `--unique` | false | NPC is unique |

**Note:** This creates an NPC record structure. NPCs need race/face data to be visible in-game. Best used for modifying existing NPCs via scripts.

**Examples:**
```bash
# Basic NPC
esp add-npc "MyMod.esp" "MyMod_Guard" --name "Test Guard" --level 25

# Essential unique NPC
esp add-npc "MyMod.esp" "MyMod_Merchant" --name "Bob the Merchant" --level 10 --essential --unique
```

---

### add-book

Add a book record to a plugin.

```bash
esp add-book <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the book |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--text` | - | Book content |
| `--value` | 10 | Gold value |
| `--weight` | 1 | Weight |

**Examples:**
```bash
# Lore book
esp add-book "MyMod.esp" "MyMod_Lore" --name "The History of Testing" --text "Long ago, in a land far away..." --value 50

# Journal
esp add-book "MyMod.esp" "MyMod_Journal" --name "Adventurer's Journal" --text "Day 1: I set out from Whiterun today..." --value 5 --weight 0.5
```

---

### add-leveled-item

Add a leveled item list to a plugin for random loot distribution.

```bash
esp add-leveled-item <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the leveled item |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--chance-none` | 0 | Percentage chance (0-100) the list returns nothing |
| `--add-entry` | - | Add entry in format: `item,level,count` (can use multiple times) |
| `--preset` | - | Apply preset configuration (see below) |
| `--calculate-each` | false | Roll separately for each item count |
| `--use-all` | false | Give all items in list |
| `--dry-run` | false | Preview changes without saving |

**Presets:**
| Preset | Description |
|--------|-------------|
| `low-treasure` | 25% chance none, good for starter areas |
| `medium-treasure` | 15% chance none, standard dungeon loot |
| `high-treasure` | 5% chance none, boss chest rewards |
| `guaranteed-loot` | 0% chance none, always gives all items |

**Examples:**
```bash
# Using preset for treasure chest
esp add-leveled-item "MyMod.esp" "MyMod_TreasureChest" --preset low-treasure

# Custom list with entries
esp add-leveled-item "MyMod.esp" "MyMod_BossLoot" --chance-none 5 --add-entry "GoldBase,1,100" --add-entry "LockPick,5,3"

# Guaranteed multi-item drop
esp add-leveled-item "MyMod.esp" "MyMod_QuestReward" --chance-none 0 --use-all --add-entry "WeaponSword,10,1"
```

---

### add-keyword

Add a Keyword (KYWD) record to a plugin.

Keywords are minimal records whose value is as a stable tag other systems can match on. The main use is with a runtime patcher such as SkyPatcher: define a keyword here, then have SkyPatcher attach it onto records your plugin does not own, letting you classify third-party items without editing or overriding them.

```bash
esp add-keyword <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the keyword |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--dry-run` | false | Preview changes without saving |

Fails if a keyword with that EditorID already exists in the plugin, since a patcher matching by EditorID would otherwise bind to an arbitrary one of the duplicates.

**Example:**
```bash
esp add-keyword "MyMod.esp" "MyMod_RestoreHealth"
```

Then, in a SkyPatcher config, tag every ingestible using Skyrim's RestoreHealth effect:
```
filterByMgefs=Skyrim.esm|0003EB15:keywordsToAdd=MyMod_RestoreHealth
```

**Implementation note:** unlike most Mutagen groups, `SkyrimGroup<Keyword>` exposes no `AddNew()` - the record is constructed against the mod (which assigns the FormKey) and then added to the group. `Keyword.Color` is a `System.Drawing.Color?`, not a uint.

---

### add-form-list

Add a form list to a plugin for script property collections.

```bash
esp add-form-list <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the form list |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--add-form` | - | Add form by EditorID or FormKey (can use multiple times) |
| `--dry-run` | false | Preview changes without saving |

**Examples:**
```bash
# Add vanilla forms by FormKey
esp add-form-list "MyMod.esp" "MyMod_MetalKeywords" --add-form "000896:Skyrim.esm" --add-form "000897:Skyrim.esm"

# Add mod-specific forms by EditorID
esp add-form-list "MyMod.esp" "MyMod_CustomWeapons" --add-form "MyMod_Sword01" --add-form "MyMod_Sword02"

# Use in script: FormList Property MyList Auto
# Set property: --property MyList --value "MyMod.esp|0x800"
```

---

### add-encounter-zone

Add an encounter zone to control level scaling in locations.

```bash
esp add-encounter-zone <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the encounter zone |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--min-level` | 1 | Minimum enemy level |
| `--max-level` | 0 | Maximum enemy level (0 = unlimited scaling) |
| `--never-resets` | false | Enemies stay defeated permanently |
| `--disable-combat-boundary` | false | NPCs can pursue player anywhere |
| `--preset` | - | Apply preset configuration (see below) |
| `--dry-run` | false | Preview changes without saving |

**Presets:**
| Preset | Description |
|--------|-------------|
| `low-level` | Min 1, Max 10 - starter areas, tutorial content |
| `mid-level` | Min 10, Max 30 - standard dungeons, mid-game |
| `high-level` | Min 30, Max 50 - end-game content, boss fights |
| `scaling` | Min 1, Max unlimited - quest content that works at any level |

**Examples:**
```bash
# Starter dungeon
esp add-encounter-zone "MyMod.esp" "MyMod_StarterCave" --preset low-level

# End-game raid
esp add-encounter-zone "MyMod.esp" "MyMod_BossLair" --min-level 30 --max-level 50 --never-resets

# Fully scaling quest zone
esp add-encounter-zone "MyMod.esp" "MyMod_QuestZone" --preset scaling
```

---

### add-location

Add a location record for named areas, quest markers, and fast travel points.

```bash
esp add-location <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the location |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name shown to player |
| `--parent-location` | - | Parent location FormKey or EditorID |
| `--add-keyword` | - | Add location keyword (can use multiple times) |
| `--preset` | - | Apply preset with keyword (see below) |
| `--dry-run` | false | Preview changes without saving |

**Presets:**
| Preset | Keyword | Use Case |
|--------|---------|----------|
| `inn` | LocTypeInn | Taverns, inns, drinking establishments |
| `city` | LocTypeCity | Major walled settlements, capitals |
| `dungeon` | LocTypeDungeon | Caves, ruins, underground areas |
| `dwelling` | LocTypeDwelling | Player homes, NPC houses |

**Examples:**
```bash
# Inn location
esp add-location "MyMod.esp" "MyMod_RustyTankard" --name "The Rusty Tankard" --preset inn

# Player home with parent
esp add-location "MyMod.esp" "MyMod_PlayerHome" --name "Cozy Cottage" --preset dwelling --parent-location "WhiterunHoldLocation"

# Custom dungeon
esp add-location "MyMod.esp" "MyMod_AncientRuins" --name "Forgotten Crypt" --preset dungeon
```

---

### add-outfit

Add an outfit record to define NPC equipment sets.

```bash
esp add-outfit <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the outfit |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--add-item` | - | Add armor or weapon by EditorID (can use multiple times) |
| `--preset` | - | Apply preset outfit (see below) |
| `--dry-run` | false | Preview changes without saving |

**Presets:**
| Preset | Contents |
|--------|----------|
| `guard` | Iron armor + sword + shield (basic guard outfit) |
| `farmer` | Farmer clothes + roughspun tunic (civilian clothing) |
| `mage` | Mage robes + hood (spellcaster outfit) |
| `thief` | Leather armor set (rogue/thief outfit) |

**Examples:**
```bash
# Guard outfit with preset
esp add-outfit "MyMod.esp" "MyMod_TownGuard" --preset guard

# Custom outfit
esp add-outfit "MyMod.esp" "MyMod_CustomOutfit" --add-item "ArmorIronCuirass" --add-item "WeaponIronSword" --add-item "ArmorIronShield"

# Farmer outfit
esp add-outfit "MyMod.esp" "MyMod_Villager" --preset farmer
```

---

### add-perk

Add a perk record to a plugin.

```bash
esp add-perk <plugin> <editorId> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |
| `editorId` | Unique Editor ID for the perk |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--name` | - | Display name |
| `--description` | - | Perk description |
| `--playable` | false | Perk can be selected by player |
| `--hidden` | false | Perk is hidden |
| `--effect` | - | Effect preset (see below) |
| `--bonus` | 25 | Bonus percentage |

**Effect Presets:**
| Preset | Description |
|--------|-------------|
| `weapon-damage` | Increase weapon damage by X% |
| `damage-reduction` | Reduce incoming damage by X% |
| `armor` | Increase armor rating by X% |
| `spell-cost` | Reduce spell cost by X% |
| `spell-power` | Increase spell magnitude by X% |
| `spell-duration` | Increase spell duration by X% |
| `sneak-attack` | Increase sneak attack multiplier |
| `pickpocket` | Increase pickpocket chance by X% |
| `prices` | Improve buying/selling prices by X% |

**Examples:**
```bash
# Combat perk - +25% weapon damage
esp add-perk "MyMod.esp" "MyMod_Damage" --name "Heavy Hitter" --description "Deal 25% more damage" --effect weapon-damage --bonus 25 --playable

# Defensive perk - 15% damage reduction
esp add-perk "MyMod.esp" "MyMod_Tank" --name "Thick Skin" --description "Take 15% less damage" --effect damage-reduction --bonus 15 --playable

# Magic perk - 20% cheaper spells
esp add-perk "MyMod.esp" "MyMod_Efficient" --name "Efficient Casting" --description "Spells cost 20% less" --effect spell-cost --bonus 20 --playable

# Stealth perk - 50% higher sneak attack damage
esp add-perk "MyMod.esp" "MyMod_Assassin" --name "Assassin's Strike" --description "Sneak attacks deal 50% more damage" --effect sneak-attack --bonus 50 --playable

# Hidden perk (for internal mechanics)
esp add-perk "MyMod.esp" "MyMod_InternalPerk" --name "Internal Effect" --effect damage-reduction --bonus 10 --hidden
```

**Note:** Without `--effect`, the perk will exist but do nothing. Always specify an effect.

---

### attach-script

Attach a Papyrus script to a quest.

```bash
esp attach-script <plugin> --quest <questId> --script <scriptName>
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Required Options:**
| Option | Description |
|--------|-------------|
| `--quest` | Editor ID of the quest |
| `--script` | Name of the script (without .pex) |

**Example:**
```bash
esp attach-script "MyMod.esp" --quest "MyMod_MainQuest" --script "MyMod_MainQuestScript"
```

---

### add-alias

Add a reference alias to a quest, optionally with a script attached. The script is stored in the quest's `VirtualMachineAdapter.Aliases[]` (QuestFragmentAlias) so the Creation Kit recognizes it.

```bash
esp add-alias <plugin> --quest <questId> --name <aliasName> [--script <scriptName>] [--flags <flags>] [--dry-run]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Required Options:**
| Option | Description |
|--------|-------------|
| `--quest` | Editor ID of the quest |
| `--name` | Name of the alias to add |

**Optional Options:**
| Option | Description |
|--------|-------------|
| `--script` | Script to attach to the new alias (without .pex) |
| `--flags` | Comma-separated `QuestAlias.Flag` values |
| `--dry-run` | Preview changes without saving |

**Common flag values:** `Optional`, `AllowReuseInQuest`, `AllowReserved`, `Essential`, `Protected`, `StoresText`, `AllowDeadActor`, `ClearsNameWhenRemoved`

**Examples:**
```bash
# Add an alias with a script in one shot
esp add-alias "FollowerMod.esp" --quest "FM_MainQuest" --name "FollowerAlias01" \
    --script "FM_FollowerAliasScript" --flags "Optional,AllowReuseInQuest,AllowReserved"

# Add an empty alias
esp add-alias "MyMod.esp" --quest "MyQuest" --name "TargetActor" --flags "Optional"
```

---

### attach-alias-script

Attach a Papyrus script to an existing quest alias. Use this when the alias already exists and you want to add (or add another) script to it.

```bash
esp attach-alias-script <plugin> --quest <questId> --alias <aliasName> --script <scriptName> [--dry-run]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Required Options:**
| Option | Description |
|--------|-------------|
| `--quest` | Editor ID of the quest |
| `--alias` | Name of the alias to attach to (must already exist) |
| `--script` | Name of the script (without .pex) |

**Optional Options:**
| Option | Description |
|--------|-------------|
| `--dry-run` | Preview changes without saving |

**Example:**
```bash
esp attach-alias-script "FollowerMod.esp" --quest "FM_MainQuest" \
    --alias "FollowerAlias01" --script "FM_FollowerAliasScript"
```

**Notes:**
- The alias must already exist on the quest (use `esp add-alias` first)
- Alias names are case-sensitive
- Setting properties on the script after attaching: use `esp set-property --alias-target <aliasName>`

---

### set-property

Manually set a script property on a quest script or alias script.

```bash
esp set-property <plugin> --quest <questId> --script <scriptName> --property <propertyName> --type <type> --value <value> [--alias-target <aliasName>]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Required Options:**
| Option | Description |
|--------|-------------|
| `--quest` | Editor ID of the quest |
| `--script` | Name of the script (without .pex) |
| `--property` | Name of the property to set |
| `--type` | Property type: `object`, `alias`, `int`, `float`, `bool`, `string` |
| `--value` | Property value (format depends on type) |

**Optional Options:**
| Option | Description |
|--------|-------------|
| `--alias-target` | Target alias name (for setting properties on alias scripts) |

**Property Types and Value Formats:**

| Type | Value Format | Example |
|------|--------------|---------|
| `object` | `Plugin.esp\|0xFormID` | `Skyrim.esm\|0x00013794` |
| `alias` | Alias name within same quest | `MyAlias` |
| `int` | Integer value | `42` |
| `float` | Float value | `3.14` |
| `bool` | `true` or `false` | `true` |
| `string` | String value | `Hello World` |

**When to Use:**
- Manually override auto-fill results
- Set properties that don't match naming conventions
- Reference YOUR mod's records (auto-fill searches Skyrim.esm)
- Set alias properties within a quest

**Important:**
- The script must already be attached to the quest/alias
- For object properties, the referenced plugin is added as a master
- For alias properties, the alias must exist in the quest
- Property names are case-sensitive

**Examples:**

```bash
# Set an object property (FormKey reference)
esp set-property "MyMod.esp" --quest "MyQuest" --script "MyScript" \
  --property "MyKeyword" --type object --value "Skyrim.esm|0x00013794"

# Set an alias property (reference to alias in same quest)
esp set-property "MyMod.esp" --quest "MyQuest" --script "MyScript" \
  --property "FollowerAlias" --type alias --value "MyFollowerAlias"

# Set integer property
esp set-property "MyMod.esp" --quest "MyQuest" --script "MyScript" \
  --property "MaxCount" --type int --value "10"

# Set float property
esp set-property "MyMod.esp" --quest "MyQuest" --script "MyScript" \
  --property "Duration" --type float --value "5.5"

# Set boolean property
esp set-property "MyMod.esp" --quest "MyQuest" --script "MyScript" \
  --property "IsEnabled" --type bool --value "true"

# Set string property
esp set-property "MyMod.esp" --quest "MyQuest" --script "MyScript" \
  --property "Message" --type string --value "Welcome!"

# Set property on alias script
esp set-property "MyMod.esp" --quest "MyQuest" --script "AliasScript" \
  --alias-target "FollowerAlias" --property "MyProp" --type int --value "5"
```

**Console Output:**
```
Saved plugin: MyMod.esp
Set int property 'MaxCount' = '10' on script 'MyScript' (quest 'MyQuest')
```

**JSON Output:**
```json
{
  "success": true,
  "result": {
    "quest": "MyQuest",
    "script": "MyScript",
    "property": "MaxCount",
    "type": "int",
    "value": "10"
  },
  "error": null
}
```

**Tip:** Use `esp auto-fill` for automatic property resolution from PSC files. Only use `set-property` for manual overrides or special cases.

---

### generate-seq

Generate a SEQ file for start-enabled quests.

```bash
esp generate-seq <plugin> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--output`, `-o` | `.` | Output directory |

**Note:** SEQ files are required for quests with `--start-enabled`. Place the SEQ file in `Data/SEQ/`.

**Example:**
```bash
esp generate-seq "MyMod.esp" --output "./SEQ"
```

---

### list-masters

List master file dependencies.

```bash
esp list-masters <plugin>
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Example:**
```bash
esp list-masters "MyMod.esp"
```

---

### merge

Merge records from one plugin into another.

```bash
esp merge <source> <target> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `source` | Source plugin to copy from |
| `target` | Target plugin to copy into |

**Options:**
| Option | Default | Description |
|--------|---------|-------------|
| `--output` | target | Output path (defaults to overwriting target) |

**Example:**
```bash
# Merge Source.esp into Target.esp, output to Merged.esp
esp merge "Source.esp" "Target.esp" --output "Merged.esp"
```

---

## JSON Output

All commands support `--json` for machine-readable output:

```bash
esp info "MyMod.esp" --json
```

**Success response:**
```json
{
  "success": true,
  "result": {
    "fileName": "MyMod.esp",
    "isLight": true,
    "totalRecords": 5
  }
}
```

**Error response:**
```json
{
  "success": false,
  "error": "File not found",
  "suggestions": ["Check the file path"]
}
```

---

### view-record

View detailed information about a specific record in a plugin.

```bash
esp view-record <plugin> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to the plugin file |

**Options:**
| Option | Description |
|--------|-------------|
| `--editor-id` | EditorID of the record to view |
| `--form-id` | FormID of the record (e.g., "0x000800") |
| `--type` | Record type (required with --editor-id) |
| `--include-raw` | Include raw properties via reflection |

**Supported Record Types:**
spell, weapon, armor, quest, npc, perk, faction, book, miscitem, global, leveleditem, formlist, outfit, location, encounterzone

**Examples:**
```bash
# View spell by EditorID
esp view-record "MyMod.esp" --editor-id "MySpell" --type spell

# View weapon by FormID
esp view-record "Skyrim.esm" --form-id "0x00012EB7"

# View with JSON output
esp view-record "MyMod.esp" --editor-id "MyQuest" --type quest --json
```

**JSON Output:**
```json
{
  "success": true,
  "result": {
    "editorId": "MySpell",
    "formKey": "MyMod.esp:0x000800",
    "recordType": "Spell",
    "properties": {
      "name": "My Custom Spell",
      "type": "Spell",
      "baseCost": 50,
      "castType": "FireAndForget",
      "targetType": "Self",
      "effectCount": 1
    }
  }
}
```

---

### update-spell

Create an ESL override patch that changes selected fields on an existing spell. The source plugin is read-only; output must be a new `.esp` or `.esl` path, and an existing output file is never overwritten. Unspecified fields, effects, and scripts are preserved.

```bash
esp update-spell <source> --output <new-patch.esp> --editor-id <editorId> [--type spell|power|lesser-power|ability] [--equipment-type <FORMID:MODNAME>] [--magic-skill <school>]
```

At least one of `--type`, `--equipment-type`, or `--magic-skill` is required. Equipment type uses a FormKey such as `013F44:Skyrim.esm`. `--magic-skill` sets the school on each distinct Magic Effect record used by the spell and includes those overrides in the same patch. Each effect must be defined in the source plugin; changing a shared effect also changes it anywhere else it is used. Supported values are `None`, `Alteration`, `Conjuration`, `Destruction`, `Illusion`, and `Restoration`.

```bash
# Convert a VRIK-style calibration power into a normal hand-equipped spell
esp update-spell "VRIK.esp" --output "esp-workspace/output/VRIKSpellPatch.esp" --editor-id "_vrik_spel_calibrate" --type spell --equipment-type "013F44:Skyrim.esm"

# Also set the associated Magic Effect school and include its override
esp update-spell "VRIK.esp" --output "esp-workspace/output/VRIKSpellPatch.esp" --editor-id "_vrik_spel_calibrate" --type spell --equipment-type "013F44:Skyrim.esm" --magic-skill Alteration
```

The patch includes the source plugin and its listed masters and must load after them.

---

### create-override

Create an override patch for a record from another plugin.

```bash
esp create-override <source> --output <output> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `source` | Path to the source plugin |

**Options:**
| Option | Description |
|--------|-------------|
| `--output`, `-o` | Name of the output patch plugin (required) |
| `--editor-id` | EditorID of the record to override |
| `--form-id` | FormID of the record to override |
| `--type` | Record type (required with --editor-id) |
| `--data-folder` | Data folder path (defaults to source directory) |

**How it works:**
1. Loads the source plugin as read-only
2. Finds the specified record
3. Creates a new patch plugin with source as master
4. Deep copies the record into the patch
5. The patch plugin loads after the source in load order

**Examples:**
```bash
# Override a spell
esp create-override "MyMod.esp" -o "MyMod_Patch.esp" --editor-id "MySpell" --type spell

# Override by FormID
esp create-override "Skyrim.esm" -o "Vanilla_Patch.esp" --form-id "Skyrim.esm:0x00012EB7"

# Override with custom data folder
esp create-override "MyMod.esp" -o "Patch.esp" --editor-id "IronSword" --type weapon --data-folder "C:\Skyrim\Data"
```

**JSON Output:**
```json
{
  "success": true,
  "result": "C:\\Skyrim\\Data\\MyMod_Patch.esp"
}
```

---

### find-record

Find records across one or multiple plugins.

```bash
esp find-record [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--search` | Search pattern for EditorID or Name |
| `--editor-id` | Exact EditorID to find |
| `--type` | Record type to filter by |
| `--plugin` | Path to specific plugin to search |
| `--data-folder` | Data folder to search all plugins |
| `--all-plugins` | Search all plugins in data folder |

**Examples:**
```bash
# Find all weapons containing "Iron"
esp find-record --search "Iron" --type weapon --plugin "Skyrim.esm"

# Find specific spell across all plugins
esp find-record --editor-id "Flames" --data-folder "C:\Skyrim\Data" --all-plugins

# Find all spells with pattern
esp find-record --search "Fire" --type spell --data-folder "C:\Skyrim\Data" --all-plugins --json
```

**JSON Output:**
```json
{
  "success": true,
  "result": [
    {
      "pluginName": "Skyrim.esm",
      "editorId": "IronSword",
      "formKey": "Skyrim.esm:0x00012EB7",
      "recordType": "Weapon",
      "name": "Iron Sword"
    }
  ]
}
```

---

### batch-override

Create override patches for multiple records at once.

```bash
esp batch-override <source> --output <output> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `source` | Path to the source plugin |

**Options:**
| Option | Description |
|--------|-------------|
| `--output`, `-o` | Name of the output patch plugin (required) |
| `--type` | Record type to filter |
| `--search` | Search pattern for EditorIDs |
| `--editor-ids` | Comma-separated list of EditorIDs |
| `--data-folder` | Data folder path |

**Examples:**
```bash
# Override all spells matching pattern
esp batch-override "MyMod.esp" -o "Spells_Patch.esp" --search "Fire*" --type spell

# Override specific weapons
esp batch-override "Skyrim.esm" -o "Weapons_Patch.esp" --editor-ids "IronSword,SteelSword,Dagger" --type weapon

# Override all globals
esp batch-override "MyMod.esp" -o "Globals_Patch.esp" --type global
```

**JSON Output:**
```json
{
  "success": true,
  "result": {
    "recordsModified": 3,
    "modifiedRecords": ["IronSword", "SteelSword", "Dagger"],
    "patchPath": "C:\\Skyrim\\Data\\Weapons_Patch.esp"
  }
}
```

---

### batch-rename

Create override patches for multiple records, appending a suffix to their display Name. Currently only `weapon` records are supported.

```bash
esp batch-rename <source> --output <output> --type weapon --editor-ids <ids> --append-suffix <suffix> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `source` | Path to the source plugin |

**Options:**
| Option | Required | Description |
|--------|----------|-------------|
| `--output`, `-o` | Yes | Name of the output patch plugin |
| `--type` | Yes | Record type to filter (only `weapon` supported) |
| `--append-suffix` | Yes | Text appended to the end of each record's existing Name |
| `--editor-ids` | No | Comma-separated list of EditorIDs to rename |
| `--data-folder` | No | Data folder path |

**Example:**
```bash
# Fix enchanted weapon names missing a "(Reversed)" suffix
esp batch-rename "ReversedDaggers VR.esp" -o "NameFix.esp" --type weapon \
  --editor-ids "ReverseEnchIronDaggerFire01,ReverseEnchIronDaggerFire02" \
  --append-suffix " (Reversed)"
```

EditorIDs not found, or not weapons, are skipped and logged rather than failing the whole batch.

---

### compare-record

Compare two versions of the same record.

```bash
esp compare-record <plugin1> <plugin2> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin1` | Path to first plugin |
| `plugin2` | Path to second plugin |

**Options:**
| Option | Description |
|--------|-------------|
| `--editor-id` | EditorID of the record to compare |
| `--form-id` | FormID of the record to compare |
| `--type` | Record type (required with --editor-id) |

**Examples:**
```bash
# Compare spell between original and patch
esp compare-record "MyMod.esp" "MyMod_Patch.esp" --editor-id "MySpell" --type spell

# Compare by FormID
esp compare-record "Skyrim.esm" "Unofficial Patch.esp" --form-id "Skyrim.esm:0x00012EB7"

# JSON output for AI analysis
esp compare-record "Original.esp" "Modified.esp" --editor-id "IronSword" --type weapon --json
```

**Console Output:**
```
Comparing: MySpell (MyMod.esp:0x000800)
Plugin 1: MyMod.esp
Plugin 2: MyMod_Patch.esp

Found 2 difference(s):

Field: BaseCost
  Original:  50
  Modified:  25

Field: TargetType
  Original:  Self
  Modified:  Touch
```

**JSON Output:**
```json
{
  "success": true,
  "result": {
    "original": {
      "editorId": "MySpell",
      "formKey": "MyMod.esp:0x000800",
      "properties": { "baseCost": 50, "targetType": "Self" }
    },
    "modified": {
      "editorId": "MySpell",
      "formKey": "MyMod.esp:0x000800",
      "properties": { "baseCost": 25, "targetType": "Touch" }
    },
    "differences": {
      "BaseCost": {
        "field": "BaseCost",
        "originalValue": 50,
        "modifiedValue": 25
      }
    }
  }
}
```

---

### conflicts

Detect load order conflicts for a record or plugin.

```bash
esp conflicts <data-folder> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `data-folder` | Path to Skyrim Data folder |

**Options:**
| Option | Description |
|--------|-------------|
| `--plugin` | Check conflicts for this plugin |
| `--editor-id` | Check conflicts for this EditorID |
| `--form-id` | Check conflicts for this FormID |
| `--type` | Record type (required with --editor-id) |

**How it works:**
1. Scans all plugins in load order (.esm → .esp → .esl)
2. Identifies which plugins modify the target record
3. Reports load order positions
4. Identifies the "winner" (last in load order)

**Examples:**
```bash
# Check conflicts for specific weapon
esp conflicts "C:\Skyrim\Data" --editor-id "IronSword" --type weapon

# Check conflicts by FormID
esp conflicts "C:\Skyrim\Data" --form-id "Skyrim.esm:0x00012EB7"

# Check all conflicts for a plugin
esp conflicts "C:\Skyrim\Data" --plugin "MyMod.esp" --json
```

**Console Output:**
```
Conflict Report:
EditorID: IronSword
FormKey: Skyrim.esm:0x00012EB7

Found 3 plugin(s) modifying this record:

[000] Skyrim.esm
[042] WeaponBalance.esp
[087] MyWeaponTweaks.esp [WINNER]

Winning override: MyWeaponTweaks.esp
```

**JSON Output:**
```json
{
  "success": true,
  "result": {
    "formKey": "Skyrim.esm:0x00012EB7",
    "editorId": "IronSword",
    "conflicts": [
      { "pluginName": "Skyrim.esm", "loadOrder": 0, "isWinner": false },
      { "pluginName": "WeaponBalance.esp", "loadOrder": 42, "isWinner": false },
      { "pluginName": "MyWeaponTweaks.esp", "loadOrder": 87, "isWinner": true }
    ],
    "winningPlugin": "MyWeaponTweaks.esp"
  }
}
```

---

### list-conditions

List all conditions on a record (Perk, Package, IdleAnimation, MagicEffect only).

```bash
esp list-conditions <plugin> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `plugin` | Path to plugin file |

**Options:**
| Option | Description |
|--------|-------------|
| `--editor-id` | EditorID of the record |
| `--form-id` | FormID of the record (alternative to EditorID) |
| `--type` | Record type (required with --editor-id): perk, package, idle, magiceffect |

**Examples:**
```bash
# List conditions on a perk by EditorID
esp list-conditions "MyMod.esp" --editor-id "MyPerk" --type perk

# List conditions using FormID
esp list-conditions "MyMod.esp" --form-id "000800:MyMod.esp"

# Get JSON output for parsing
esp list-conditions "MyMod.esp" --editor-id "MyPerk" --type perk --json
```

**Output:**
```
Found 2 condition(s):

[0] GetLevel
    Operator: GreaterThanOrEqualTo
    Comparison: 10
    Flags: 0
    RunOn: Subject

[1] IsSneaking
    Operator: EqualTo
    Comparison: 1
    Flags: 0
    RunOn: Subject
```

**JSON Output:**
```json
{
  "success": true,
  "result": [
    {
      "functionName": "GetLevel",
      "comparisonValue": 10,
      "operator": "GreaterThanOrEqualTo",
      "flags": "0",
      "runOn": "Subject"
    },
    {
      "functionName": "IsSneaking",
      "comparisonValue": 1,
      "operator": "EqualTo",
      "flags": "0",
      "runOn": "Subject"
    }
  ]
}
```

**Notes:**
- Only works on record types that support conditions
- Use index numbers from output for remove-condition command
- Conditions are numbered starting from 0

---

### add-condition

Add a condition to a record (creates override patch).

```bash
esp add-condition <source> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `source` | Path to source plugin |

**Options:**
| Option | Required | Description |
|--------|----------|-------------|
| `--output`, `-o` | Yes | Name of output patch plugin |
| `--editor-id` | Conditional | EditorID of record (requires --type) |
| `--form-id` | Conditional | FormID of record (alternative to EditorID) |
| `--type` | Conditional | Record type (required with --editor-id) |
| `--function` | Yes | Condition function name (e.g., GetLevel, IsSneaking) |

**Common Condition Functions:**
- `GetLevel` - Check player/actor level
- `IsSneaking` - Check if sneaking
- `IsRunning` - Check if running
- `IsSwimming` - Check if swimming
- `IsInCombat` - Check if in combat
- `GetActorValue` - Check actor value (requires parameters)
- See Mutagen documentation for full list

**Examples:**
```bash
# Add level requirement to perk
esp add-condition "MyMod.esp" -o "MyMod_Patch.esp" \
  --editor-id "MyPerk" --type perk --function GetLevel

# Add sneak condition using FormID
esp add-condition "MyMod.esp" -o "Patch.esp" \
  --form-id "000800:MyMod.esp" --function IsSneaking

# Add condition with JSON output
esp add-condition "MyMod.esp" -o "Patch.esp" \
  --editor-id "MyPerk" --type perk --function IsInCombat --json
```

**Output:**
```
Created patch with new GetLevel condition:
MyMod_Patch.esp
```

**JSON Output:**
```json
{
  "success": true,
  "result": "MyMod_Patch.esp"
}
```

**Notes:**
- Creates new patch plugin with source as master
- Comparison value defaults to 1.0 (true/false)
- Comparison operator defaults to >= (GreaterThanOrEqualTo)
- For custom values, manually edit patch or use remove+add sequence
- Only works on Perk, Package, IdleAnimation, MagicEffect records

---

### remove-condition

Remove specific conditions from a record (creates override patch).

```bash
esp remove-condition <source> [options]
```

**Arguments:**
| Argument | Description |
|----------|-------------|
| `source` | Path to source plugin |

**Options:**
| Option | Required | Description |
|--------|----------|-------------|
| `--output`, `-o` | Yes | Name of output patch plugin |
| `--editor-id` | Conditional | EditorID of record (requires --type) |
| `--form-id` | Conditional | FormID of record (alternative) |
| `--type` | Conditional | Record type (required with --editor-id) |
| `--indices` | Yes | Comma-separated indices to remove (e.g., "0,2,5") |

**Examples:**
```bash
# Remove first condition (index 0)
esp remove-condition "MyMod.esp" -o "Patch.esp" \
  --editor-id "MyPerk" --type perk --indices "0"

# Remove multiple conditions
esp remove-condition "MyMod.esp" -o "Patch.esp" \
  --form-id "000800:MyMod.esp" --indices "0,2,5"

# Remove all but one (if record has 3 conditions, remove 1 and 2)
esp remove-condition "MyMod.esp" -o "Patch.esp" \
  --editor-id "MyPerk" --type perk --indices "1,2"
```

**Output:**
```
Created patch with 2 condition(s) removed:
MyMod_Patch.esp
```

**JSON Output:**
```json
{
  "success": true,
  "result": "MyMod_Patch.esp"
}
```

**Notes:**
- Use list-conditions first to see indices
- Indices are 0-based (first condition is 0)
- Creates new patch plugin with source as master
- Can remove multiple conditions in single operation
- Specify indices in any order (e.g., "2,0,1" works same as "0,1,2")

---

## Workflow Examples

### Creating an Override Patch

Common workflow for modifying an existing record:

```bash
# 1. View the original record
esp view-record "OriginalMod.esp" --editor-id "MySpell" --type spell

# 2. Create an override patch
esp create-override "OriginalMod.esp" -o "MyTweaks.esp" --editor-id "MySpell" --type spell

# 3. Verify the override was created
esp view-record "MyTweaks.esp" --editor-id "MySpell" --type spell

# 4. Compare to see they match
esp compare-record "OriginalMod.esp" "MyTweaks.esp" --editor-id "MySpell" --type spell
```

### Batch Patching Multiple Records

```bash
# Find all fire spells
esp find-record --search "Fire" --type spell --plugin "Skyrim.esm"

# Create overrides for all matching spells
esp batch-override "Skyrim.esm" -o "FireSpells_Patch.esp" --search "Fire*" --type spell

# Check for conflicts
esp conflicts "C:\Skyrim\Data" --plugin "FireSpells_Patch.esp"
```

### Conflict Resolution

```bash
# Find which plugins modify a record
esp conflicts "C:\Skyrim\Data" --editor-id "IronSword" --type weapon

# Compare the winning version vs original
esp compare-record "Skyrim.esm" "WinningMod.esp" --editor-id "IronSword" --type weapon

# Create your own override to win
esp create-override "Skyrim.esm" -o "MyFinalTweak.esp" --editor-id "IronSword" --type weapon
```

### Condition Management Workflow

```bash
# 1. List existing conditions on a perk
esp list-conditions "MyMod.esp" --editor-id "MyPerk" --type perk

# 2. Add a new level requirement
esp add-condition "MyMod.esp" -o "MyMod_Conditioned.esp" \
  --editor-id "MyPerk" --type perk --function GetLevel

# 3. Verify the condition was added
esp list-conditions "MyMod_Conditioned.esp" --editor-id "MyPerk" --type perk

# 4. Remove unwanted conditions (e.g., remove index 0)
esp remove-condition "MyMod_Conditioned.esp" -o "MyMod_Final.esp" \
  --editor-id "MyPerk" --type perk --indices "0"

# 5. Final verification
esp list-conditions "MyMod_Final.esp" --editor-id "MyPerk" --type perk
```

### Removing Spell Conditions from Existing Mod

Common use case for removing targeting restrictions:

```bash
# 1. View the spell to understand structure
esp view-record "SomeMod.esp" --editor-id "ProblematicSpell" --type spell

# 2. Check what's in the mod (spells don't have conditions directly)
# Note: Conditions are on Perk/Package/MagicEffect, not Spell records

# 3. If targeting is via MagicEffect, find the effect FormID
esp view-record "SomeMod.esp" --editor-id "ProblematicSpell" --type spell --json

# 4. View the magic effect conditions
esp list-conditions "SomeMod.esp" --form-id "000XXX:SomeMod.esp"

# 5. Remove targeting conditions
esp remove-condition "SomeMod.esp" -o "SomeMod_Patch.esp" \
  --form-id "000XXX:SomeMod.esp" --indices "0,1"
```
