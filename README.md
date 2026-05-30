# Error Fix

Error Fix is a BepInEx/Harmony compatibility guard for Lethal Company. It mitigates known runtime exceptions and log spam from recurring null-reference, index, audio, particle, NavMesh, Netcode, and optional third-party compatibility cases observed in heavily modded V81-era setups.

Version: `0.0.5`

## Scope

This project is defensive only. It does not replace game systems, rebalance enemies, change spawn tables, or intentionally alter normal AI behavior. Patches either skip an unsafe frame, validate an index or object reference before vanilla code uses it, or suppress a known exception type after the target mod or game method has already failed.

The mod limits repeated warnings through a shared warning limiter so the log remains useful while still making handled problems visible.

Error Fix is not affiliated with, endorsed by, or maintained by Zeekerss, Unity Technologies, BepInEx, Harmony, or the authors of the optional compatibility targets listed below.

## Main fixes

- Optionally guards common Unity audio warnings such as disabled audio sources and null `AudioClip` playback.
- Filters exact known Unity warning spam for missing audio spatializer plugin setup, BoxCollider negative scale/size asset warnings, disabled AudioSource playback warnings, SteamValve empty AudioSource warnings, and duplicate Static Lighting Sky warnings by default.
- Filters exact known BepInEx log noise from `RuntimeIcons`, `PathfindingLib`, and `LethalPerformance` by source, level, and message shape. Error and exception logs are not filtered.
- Optionally guards invalid particle mesh shape sources, including unreadable meshes and zero-area meshes.
- Filters unreadable mesh sources during runtime NavMesh source collection.
- Optionally adds an `EnemyAI` NavMesh fallback for agents that try to move while off the NavMesh.
- Optionally guards known Netcode edge cases around client-side ragdoll destruction, with lifecycle-aware limits; non-global Netcode guards remain active where targeted.
- Optionally guards global player ragdoll tag lookups/setters for undefined `PlayerRagdoll4+` tags without rewriting tags that are actually defined.
- Guards known item, suit, quicksand, entrance teleport, terminal, chat, ambience, jetpack, soccer ball, and enemy update errors, with per-frame hot-path replacement guards requiring explicit `Enabled`.
- Treats `Auto` as disabled for sensitive hot-path replacements such as `EntranceTeleport.Update` so the default configuration avoids added per-frame Harmony overhead.
- Keeps performance-sensitive global guards disabled by default, including `AudioSource.Play*`, global player-ragdoll tag lookup/setter hooks, the global `UnityEngine.Object.Destroy` hook, and particle mesh scene scans.
- Uses `Auto` for targeted version-aware non-hot-path replacements where the verified game assembly can be checked before patching.
- Installs Harmony patch classes independently so one failed patch target does not prevent unrelated guards from loading.
- Leaves the `EnemyAI.DoAIInterval` NavMesh guard disabled by default because it is a per-enemy AI tick hook; enable it only when the target modpack needs off-NavMesh suppression.

For the full patch index, see [PatchCatalog.md](V81ErrorFix/PatchCatalog.md).

## 0.0.5 focus

Version `0.0.5` keeps the functional Error Fix guard set while making default runtime behavior more conservative for average FPS and 99th percentile FPS. High-frequency `Update` and AI tick replacements now require explicit `Enabled`, `RuntimePatchMode` provides a master passive baseline for comparison testing, and known log-noise filtering covers the high-volume messages observed in large V81 modpacks without hiding Error or exception logs.

## Configuration notes

- Sensitive guards use `PatchEnableMode`: `Auto` enables selected version-aware non-hot-path guards on verified game assemblies, `Enabled` forces a guard on, and `Disabled` turns it off.
- `RuntimePatchMode=Disabled` is the master passive mode for FPS baseline testing. It leaves the DLL loaded but installs no Harmony patches, registers no scene lifecycle hooks, binds no extra ErrorFix settings, and skips Assembly-CSharp verification. Use this when comparing installed ErrorFix against no ErrorFix.
- `GlobalDestroyGuardMode=Enabled` is required to install the spawned ragdoll global destroy guard. `Auto` is treated as disabled for this global hook.
- `AudioSourcePlaybackGuardMode`, `PlayerRagdollGlobalTagGuardMode`, `ParticleMeshShapeGuardMode`, `PlayerNearOtherPlayersGuardMode`, `TerminalAccessibleObjectUpdateGuardMode`, `EntranceTeleportUpdateGuardMode`, `UnlockableSuitUpdateGuardMode`, `GameplayEnemyUpdateGuardMode`, `EnemyHealthBarsLateUpdateGuardMode`, `ShipLootPlusUiHelperGuardMode`, and `NightVisionInsideLightingPostfixGuardMode` require explicit `Enabled`; `Auto` is treated as disabled for these performance-sensitive global or hot-path guards.
- `KnownUnityWarningFilterMode=Enabled` is the default and installs a log-only filter for exact high-frequency Unity warning prefixes: missing audio spatializer plugin setup, BoxCollider negative scale/size asset warnings, disabled AudioSource playback warnings, SteamValve empty AudioSource warnings, and duplicate Static Lighting Sky warnings. It reports a compact per-scene summary of suppressed warning counts, does not repair the underlying plugin, collider, AudioSource, or lighting setup, and does not filter Netcode lifecycle warnings. Set it to `Disabled` while tracing the source of those warnings.
- `KnownBepInExLogNoiseFilterMode=Enabled` is the default and installs a log-only filter for exact known low-value messages from `RuntimeIcons`, `PathfindingLib`, and `LethalPerformance`. It filters by source, log level, and message shape; Error and exception logs stay visible.
- `NetworkObjectParentGuardMode=Auto` suppresses only the known unspawned Netcode reparent `SpawnStateException` on verified game assemblies.
- `SteamValveDamageTriggerSpawnGuardMode` is disabled by default. The `damageTrigger is disabled` Netcode message is usually a one-time spawn lifecycle warning rather than a performance issue; enable this experimental guard only after confirming SteamValve `damageTrigger` gameplay is actually broken.
- `EnableGlobalDestroyGuard` is retained as a legacy compatibility switch for older local configs.
- `ParticleMeshShapeGuardDryRun=true` logs invalid particle mesh shapes without disabling them. The particle mesh guard scans once after scene load when explicitly enabled, not on a periodic timer.
- Optional mod guards each have an `OptionalCompatibility` config entry and return unknown `NullReferenceException` cases to the original caller after logging a limited diagnostic.
- Performance-sensitive guards are evaluated during plugin startup. Change their config values before launching the game or restart after editing them.
- `EnemyAINavMeshGuardMode=Enabled` is required to install the `EnemyAI.DoAIInterval` hot-path guard. `Auto` is treated as disabled for this per-enemy AI tick hook. EnemyAI warp recovery remains disabled unless `AllowEnemyAIWarp=true`.
- `PlayerNearOtherPlayersGuardMode`, `TerminalAccessibleObjectUpdateGuardMode`, `EntranceTeleportUpdateGuardMode`, `UnlockableSuitUpdateGuardMode`, and `GameplayEnemyUpdateGuardMode` are disabled by default. Use `Enabled` only when one of those exact hot-path guards is needed more than the FPS cost risk.

## Optional compatibility targets

These compatibility guards are applied by reflection and only activate when the target mod type and expected method signature are present. They are not forks, redistributions, or modified builds of the listed projects. Error Fix does not bundle third-party mod DLLs, source code, package contents, or assets. Unknown optional-mod exceptions are logged and returned rather than broadly suppressed.

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
  -p:LethalCompanyDir="PATH_TO_LETHAL_COMPANY_INSTALL" `
  -p:BepInExCoreDir="PATH_TO_BEPINEX_CORE"
```

The project expects these local files:

- `BepInEx.dll` and `0Harmony.dll` under `BepInExCoreDir`.
- `Assembly-CSharp.dll`, Unity assemblies, and Netcode assemblies under `LethalCompanyDir\Lethal Company_Data\Managed`.

Do not copy BepInEx or Harmony files into the Steam game directory just to build this project.

The build uses `BepInEx.AssemblyPublicizer.MSBuild` only at compile time so selected `Assembly-CSharp` members can be accessed directly instead of through hot-path private-field reflection. The package is excluded from runtime assets and is not bundled with the mod output.

NuGet restore uses `packages.lock.json` and the repository `global.json` pins the .NET SDK used by CI. CI runs locked restore and NuGet audit, but full compilation remains a local step because the GitHub runner does not have Lethal Company or BepInEx assemblies.

`NuGet.config` restricts restore to nuget.org with package source mapping for the build-time publicizer package. Local build paths must come from MSBuild properties, environment variables, or a local untracked `Directory.Build.props`; the project no longer falls back to a machine-specific Steam install path.

Build output is written to `V81ErrorFix\build_tmpbin`. The repository intentionally does not track compiled DLLs, game assemblies, decompiled game source, backup files, archives, NuGet packages, Thunderstore package contents, third-party mod binaries, or local build artifacts.

## Repository contents

The GitHub repository is intentionally source-only and minimal:

- `V81ErrorFix/` contains the plugin source and project file.
- `V81ErrorFix.Tests/` contains local helper tests that require the same local game and BepInEx reference paths as the plugin build.
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
