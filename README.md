# Error Fix

Error Fix is a BepInEx/Harmony compatibility guard for Lethal Company. It mitigates known runtime exceptions and log spam from recurring null-reference, index, audio, particle, NavMesh, Netcode, and optional third-party compatibility cases observed in heavily modded V81-era setups.

Version: `0.0.3`

## Scope

This project is defensive only. It does not replace game systems, rebalance enemies, change spawn tables, or intentionally alter normal AI behavior. Patches either skip an unsafe frame, validate an index or object reference before vanilla code uses it, or suppress a known exception type after the target mod or game method has already failed.

The mod limits repeated warnings through a shared warning limiter so the log remains useful while still making handled problems visible.

Error Fix is not affiliated with, endorsed by, or maintained by Zeekerss, Unity Technologies, BepInEx, Harmony, or the authors of the optional compatibility targets listed below.

## Main fixes

- Guards common Unity audio warnings such as disabled audio sources and null `AudioClip` playback.
- Guards invalid particle mesh shape sources, including unreadable meshes and zero-area meshes.
- Filters unreadable mesh sources during runtime NavMesh source collection.
- Adds a general `EnemyAI` NavMesh fallback for agents that try to move while off the NavMesh.
- Guards known Netcode edge cases around client-side ragdoll destruction, unspawned object reparenting, and selected RPC null/index failures, with lifecycle-aware limits.
- Guards player ragdoll tag lookups for undefined `PlayerRagdoll4+` tags without rewriting tags that are actually defined.
- Guards known item, suit, quicksand, entrance teleport, terminal, chat, ambience, jetpack, soccer ball, and enemy update errors.
- Uses version-aware defaults for sensitive replacements such as `EntranceTeleport.Update`.
- Keeps high-risk guards in `Auto` mode by default so verified game assemblies receive the guarded path while unknown assemblies fall back to vanilla unless explicitly enabled.
- Leaves EnemyAI warp recovery disabled by default; off-NavMesh movement is still guarded, but position recovery must be enabled by configuration.

For the full patch index, see [PatchCatalog.md](V81ErrorFix/PatchCatalog.md).

## Configuration notes

- Sensitive guards use `PatchEnableMode`: `Auto` enables verified game assemblies, `Enabled` forces the guard on, and `Disabled` turns it off.
- `GlobalDestroyGuardMode` is the primary switch for the spawned ragdoll destroy guard.
- `EnableGlobalDestroyGuard` is retained as a legacy compatibility switch for older local configs.
- EnemyAI warp recovery is disabled by default; enable `AllowEnemyAIWarp` only after testing it in the target modpack.

## Optional compatibility targets

These compatibility guards are applied by reflection and only activate when the target mod type is present. They are not forks, redistributions, or modified builds of the listed projects. Error Fix does not bundle third-party mod DLLs, source code, package contents, or assets.

| Project | Compatibility case | Upstream license metadata |
| --- | --- | --- |
| EnemyHealthBars by NotezyTeam | Handles known `EnemyHealthBars.Scripts.HealthBar.LateUpdate` null-reference failures. | No explicit license identified from the public Thunderstore/source metadata at the time this README was prepared. |
| ShipLootPlus by PXC / ProfX66 | Handles known loot UI null-reference failures in `ShipLootPlus.Utils.UiHelper`, including generated loot-value lambdas and `UpdateDatapoints.MoveNext`. | AGPL-3.0, as listed by the upstream GitHub repository. Error Fix does not copy, modify, or distribute ShipLootPlus source or binaries; it only detects public runtime types by reflection. |
| ToggleableNightVision by kentrosity | Handles known `NightVision.Patches.NightVisionOutdoors.InsideLightingPostfix` null-reference failures during lighting updates. | No explicit license file identified in the linked public GitHub repository at the time this README was prepared. |
| ChatCommands by CTMods / Toemmsen96 | Handles known `ChatCommandAPI.Patches.GameNetworkManager_StartHost.Postfix` null-reference failures when hosting a lobby. | MIT, as listed by the upstream GitHub repository. |

## Build

Provide the local game path through `LethalCompanyDir` and the BepInEx core folder from a mod-manager profile through `BepInExCoreDir`:

```powershell
dotnet build .\V81ErrorFix\V81ErrorFix.csproj `
  -p:LethalCompanyDir="D:\Steam\steamapps\common\Lethal Company" `
  -p:BepInExCoreDir="D:\1New R2modman modpacks\LethalCompany\profiles\V81 Test Version 1203a2\BepInEx\core"
```

The project expects these local files:

- `BepInEx.dll` and `0Harmony.dll` under `BepInExCoreDir`.
- `Assembly-CSharp.dll`, Unity assemblies, and Netcode assemblies under `LethalCompanyDir\Lethal Company_Data\Managed`.

Do not copy BepInEx or Harmony files into the Steam game directory just to build this project.

Build output is written to `V81ErrorFix\build_tmpbin`. The repository intentionally does not track compiled DLLs, game assemblies, decompiled game source, Thunderstore package contents, third-party mod binaries, or local build artifacts.

## Repository contents

The GitHub repository is intentionally source-only and minimal:

- `V81ErrorFix/` contains the plugin source and project file.
- `.github/workflows/validate.yml` rejects generated binaries, build outputs, decompiled game source, and redistributed assemblies.
- `README.md`, `CHANGELOG.md`, `LICENSE`, and `THIRD_PARTY_NOTICES.md` provide release, license, and compatibility context.

Thunderstore package files and compiled DLLs are prepared outside the GitHub source tree.

## Installation

Build the project and place the generated `V81ErrorFix.dll` into a BepInEx plugin folder for your Lethal Company profile.

## Compatibility and support

Error Fix targets known stack traces and known exception types. It is not a general exception suppressor and does not guarantee compatibility with every game version, modpack, or mod load order.

Because the project uses Harmony patches, test new releases in a separate profile before using them in a long-running save or public modpack.

When reporting an issue, include:

- Lethal Company version.
- Error Fix version.
- Full stack trace.
- Mod list and mod loader version.
- Whether the issue happened as host, client, or in single-player.
- Steps that reproduced the issue, if known.

## Notes

- This repository contains only the Error Fix source code and documentation.
- Lethal Company game assemblies and third-party mod assemblies are not redistributed.
- Compatibility patches are intentionally narrow and return unknown exceptions to the original caller.
