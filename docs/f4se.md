# Fallout DLL projects

The `f4se` command group creates and builds C++ DLL starters using the official F4SE public PluginAPI headers. Existing `skse` (Skyrim DLL) and `fo4` (Fallout ESP) commands remain separate.

## Supported targets

| Target | Game runtime | Official SDK |
| --- | --- | --- |
| `vr` | Fallout 4 VR 1.2.72 | F4SEVR 0.6.21 |
| `desktop-legacy` | Fallout 4 1.10.163 | F4SE 0.6.23 |

Newer desktop versions are not supported by these starters. The SDK and generated runtime guard must match. The DLL rejects the editor, another runtime and an older extender.

## Local SDKs

Keep the official source archives outside the toolkit. For example:

- VR: `<F4SE SDK root>\vr-0.6.21\src\f4sevr`
- Legacy desktop: `<F4SE SDK root>\desktop-0.6.23\src\f4se`

The `--sdk` directory must contain `f4se\PluginAPI.h` and `f4se_common\f4se_version.h`; retain the sibling common source folder from the archive. No dependency downloads happen during project creation or building.

## Create and build a VR DLL

Run these with the toolkit executable (or its full path):

```powershell
spookys-automod f4se create MyFalloutVRPlugin --target vr --output "<projects folder>" --sdk "<F4SE SDK root>\vr-0.6.21\src\f4sevr" --json
spookys-automod f4se info "<projects folder>\MyFalloutVRPlugin" --json
spookys-automod f4se build "<projects folder>\MyFalloutVRPlugin" --json
```

For desktop 1.10.163, use `--target desktop-legacy` and its SDK path. Project names use letters, numbers and underscores, starting with a letter. Existing project directories are never replaced by `create`.

Generated files: `main.cpp`, `host.cpp`, `CMakeLists.txt`, `f4se-project.json`, `.gitignore`, and `README.md`. The JSON stores the absolute SDK path; update it if moving the SDK.

Build prerequisites are CMake/CTest on PATH and Visual Studio C++ Build Tools with a Windows SDK. Builds target x64 Release, run CTest, then stage `package\F4SE\Plugins\<name>.dll`. Build output is captured in `build\toolkit-build.log`; failures return nonzero exit codes and JSON errors. The DLL uses the static MSVC runtime. Build commands run only against projects you trust, since CMake projects can execute build steps.

## What the starter does

- Exports `F4SEPlugin_Query` and `F4SEPlugin_Load` with the official API signatures.
- Reports plugin metadata and checks exact runtime compatibility.
- Registers an F4SE game-loaded message callback and emits Windows debugger messages.
- Does not alter records, install hooks, modify saves, or access game memory.

Implement behavior in `main.cpp`. The public API starter needs no SDK static libraries. Advanced engine calls, address relocation, Papyrus bindings, VR input and hooks need additional matching source/library integration; this is not a ready-made CommonLibF4 framework or automatic Skyrim DLL converter.

## Verification and game testing

`host.cpp` loads the generated DLL in a standalone process and checks exports, metadata, runtime and editor rejection, old extender rejection, missing/old messaging, invalid handles, registration failure, successful load and callback invocation. CTest fails the build workflow if these checks fail. The host is test code and is not packaged with the game DLL.

Both targets compiled and passed their mock-host tests on this machine. All 87 toolkit tests passed. This verifies the starter's binary interface; it does **not** prove it loads in Fallout or that future gameplay code works.

No plugin is installed or enabled automatically. For a separate in-game test, manually install the generated package as an MO2 mod, enable it, launch the matching extender, and inspect the extender log for the plugin load result. Test DLLs have no gameplay feature. Actual in-game verification is still pending.

## SDK provenance

Official downloads: https://f4se.silverlock.org/

- VR: https://f4se.silverlock.org/beta/f4sevr_0_6_21.7z
- Legacy desktop: https://f4se.silverlock.org/beta/f4se_0_06_23.7z

Source archives are preserved locally, not embedded in the toolkit or generated package. See the SDK readmes for distribution terms; the F4SE authors request public source for distributed plugins.
