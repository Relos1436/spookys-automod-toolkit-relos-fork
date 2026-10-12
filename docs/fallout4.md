# Fallout 4 plugin operations

The `fo4` command group uses Mutagen.Bethesda.Fallout4 0.52.0. The existing `esp` commands remain Skyrim-specific. Always choose the command group explicitly; plugin extensions do not identify the game.

## Commands

All commands accept `--json` and return exit code 0 on success, 1 on an operation error.

```powershell
spookys-automod fo4 info "Source.esp" --json
spookys-automod fo4 list-records "Source.esp" --type Armor --json
spookys-automod fo4 find-record "Source.esp" --search Backpack --json
spookys-automod fo4 view-record "Source.esp" --record "000800:Source.esp" --json
spookys-automod fo4 create --output "output\NewPlugin.esp" --author YourName --json
spookys-automod fo4 add-record --output "output\KeywordPlugin.esp" --type Keyword --editor-id MyKeyword --json
spookys-automod fo4 create-override "Source.esp" --record MyArmor --output "output\ArmorPatch.esp" --changes "changes.json" --json
```

`--output` is a complete new plugin filename, not a directory. ESP, ESM and ESL names are accepted. `add-record` creates a new plugin containing one new record; it does not append to an existing plugin. Type names are Mutagen classes such as `Armor`, `Weapon`, `Keyword`, and `GlobalFloat`, not four-character record signatures. Use `list-records` to discover class names in an existing plugin.

`--record` accepts an exact EditorID (case-insensitive) or a complete FormKey. Duplicate EditorIDs require a FormKey. The FormKey includes the record's originating plugin, which can differ from the inspected plugin for overrides.

## Editing

Example `changes.json`:

```json
{
  "Name": "Renamed armor",
  "Weight": 2.5,
  "Value": 100
}
```

Use `view-record` to inspect fields before editing. Field paths are case-sensitive. Supported values are strings, numeric types, booleans, enum names and translated display strings. Dot-separated paths can traverse non-null reference objects. FormKeys and selected structural fields are protected. Invalid fields/types fail before output is published. Omitting `--changes` copies the record without edits. `add-record` also accepts `--changes` for initial field values; a newly constructed record may require additional game-specific data to be useful in-game.

Sources are read-only. Outputs must not exist, and patches must have a different filename from their source. The record is deep-copied with its original FormKey and links; Mutagen writes the required master references. Each output is written in a temporary directory, reopened to enumerate its records, then moved to the requested path without replacing an existing file.

Use copies in a staging folder, and leave completed patches in an output folder for manual installation. No commands change MO2 configuration or enable plugins.

## Current limits

- Read plugin headers and enumerate/search records, including nested records. `view-record` is a JSON inspection projection, not a lossless export/import format; nested values beyond six levels are represented as text.
- Create empty plugins, create one top-level record, or override one top-level record per output. This is initial Fallout 4 support, not parity with all Skyrim commands.
- Nested record patching (CELL/REFR/INFO and similar), batch patching, collections, FormLink editing, script attachment, condition editing, merging and specialized builders are not implemented.
- Read/write validation checks binary readability, not Creation Kit or in-game semantics. External assets and referenced masters must still be present when using the patch.
- Localized plugins may require their companion strings files; there is no CLI strings/data-folder configuration yet.
- Light flagging is opt-in with `--light`. Leave it off for Fallout 4 VR; desktop light-plugin support is not a guarantee of VR compatibility. This module targets the Fallout 4 format, not a separate VR conversion pipeline.

## Verification (2026-09-06)

- 80 automated tests passed, including 11 Fallout 4 tests covering header flags, new records, edits, FormKeys, links, masters, preservation of sources and invalid inputs.
- Read a staged copy of Backpacks of the Commonwealth: 3,782 records.
- Created and reopened an armor override changing Name and Weight. All other inspected fields matched; staged and installed source SHA-256 hashes matched.
- No game launch or Creation Kit validation performed. Validation outputs are test artifacts and should not be installed.

Mutagen reference: https://mutagen-modding.github.io/Mutagen/plugins/Copy-Functionality/
