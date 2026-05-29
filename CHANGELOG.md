# Changelog

## Unreleased

### Changed

- Changed the `EnemyAI.DoAIInterval` NavMesh guard to explicit opt-in through `EnemyAINavMeshGuardMode=Enabled`, removing its per-enemy AI tick Harmony overhead from the default configuration.
- Changed targeted per-frame hot-path guards to explicit opt-in through `Enabled`: `PlayerControllerB.NearOtherPlayers`, `TerminalAccessibleObject.Update`, `EntranceTeleport.Update`, `UnlockableSuit.Update`, and gameplay enemy `Update`/`LateUpdate` patches. `Auto` is treated as disabled for these guards to protect average FPS and 99th percentile FPS by default.
- Added `RuntimePatchMode`, a master runtime patch switch. `Disabled` leaves the plugin DLL loaded but installs no Harmony patches, scene lifecycle hooks, extra config bindings, or Assembly-CSharp verification, allowing direct FPS baseline testing against an installed-but-passive ErrorFix.
- Added exact-source BepInEx log noise filtering for high-volume `RuntimeIcons`, `PathfindingLib`, and `LethalPerformance` messages observed in the test profile. Error and exception logs are not filtered.
- Extended known Unity warning filtering to include the repeated disabled AudioSource playback warning.
- Fixed Assembly-CSharp verification when `Assembly.Location` is empty by falling back to the BepInEx managed assembly path.

## 0.0.4 - 2026-05-17

Maintenance release focused on lower runtime overhead, clearer Unity warning handling, and stricter RPC safety boundaries.

### Changed

- Added exact-prefix Unity warning filtering for missing audio spatializer setup, BoxCollider negative scale/size messages, SteamValve empty AudioSource warnings, and duplicate Static Lighting Sky warnings.
- Added a scene-transition summary for filtered Unity warnings so suppressed log volume remains visible without logging each repeated warning.
- Kept Netcode lifecycle warnings outside the known-warning filter, including `NetworkVariable` and disabled `NetworkBehaviour` spawn messages.
- Changed `SteamValveDamageTriggerSpawnGuardMode` to default to `Disabled`; the experimental `damageTrigger` spawn guard now requires explicit `Enabled`.
- Preserved ClientRpc exception suppression as Execute-stage only, so send-stage RPC send errors continue to surface normally.
- Reduced optional compatibility startup and exception-path overhead by gating target resolution earlier and caching resolved ShipLootPlus targets and formatted method signatures.
- Reduced normal-frame overhead in hot finalizers by returning before known-safe classifier delegates are created when no exception was thrown.
- Added retry cooldown for `TerminalAccessibleObject` initialization fallback so persistent missing dependencies do not trigger a per-frame retry path.
- Cleared cached entrance lookup state on scene lifecycle changes to avoid retaining stale scene objects.

## 0.0.3 - 2026-05-13

Maintenance release focused on narrowing high-risk guards and improving release build clarity.

### Changed

- Hardened `PlayerControllerB.ThrowObjectClientRpc` so partial mutation failure paths do not continue into vanilla execution with an altered RPC stage.
- Added guarded owner cleanup for throw-object failure recovery, with warning limiting if cleanup itself fails.
- Added configurable lifecycle destroy window handling for scene load, scene unload, and active-scene transitions.
- Narrowed `DeadBodyInfo.Start` out-of-range suppression to known player ragdoll/index contexts and added contextual warning details.
- Improved `TerminalAccessibleObject.InitializeValues` unknown-exception warning keys while continuing to skip unsafe vanilla retries.
- Changed performance-sensitive global guards to `Enabled`-only startup gates, including `AudioSource.Play*`, global player-ragdoll tag lookup/setter hooks, particle mesh scene scans, and the global `UnityEngine.Object.Destroy` hook.
- Treated `Auto` as disabled for those performance-sensitive global guards so existing configs do not continue installing the global destroy hook unless explicitly changed to `Enabled`.
- Added build-time `BepInEx.AssemblyPublicizer.MSBuild` usage so selected `Assembly-CSharp` fields can be accessed directly without adding runtime publicizer dependencies.
- Isolated Harmony patch installation by patch class so one failed target no longer prevents unrelated guards from loading.
- Narrowed several vanilla replacement patches to dangerous-input checks so normal Quicksand, SoccerBall, and player audio/proximity paths run through vanilla code.
- Stopped default-expanding malformed `SyncShipUnlockablesClientRpc` arrays; required malformed payloads are now logged and skipped, while invalid player suit IDs still fall back to suit 0 with slot/count diagnostics.
- Removed the `NetworkObject.OnTransformParentChanged` prefix that skipped vanilla Netcode parent cache handling.
- Added independent optional-mod compatibility config entries and known-safe null classifiers; unknown optional-mod null references are logged and returned.
- Added particle mesh dry-run diagnostics and lower-allocation mesh inspection using reusable scratch lists.
- Added NuGet lock file restore/audit checks and source-level CI checks for unsafe Harmony installation and unchecked `AccessTools.Method` yields.
- Restored the vanilla `PlayJumpAudio` default jump SFX fallback when a suit unlockable exists but its `jumpAudio` is null.
- Documented the verified V81 `ThrowObjectClientRpc` generated RPC stage lifecycle and kept handled execute-stage replacements at `Send`.
- Tightened optional-mod reflection classifiers so missing members are treated as signature mismatches instead of known-safe null values.
- Added ShipLootPlus `CalculateLootValue` compatibility for both known ignored-list signatures while logging resolved targets at info level.
- Narrowed broad `PlayerJumpedClientRpc`, `GrabObjectClientRpc`, and soccer-ball null-reference finalizers to known-safe dependency failures.
- Updated particle mesh shape inspection to scan all submeshes before declaring a mesh zero-area.

## 0.0.2 - 2026-05-13

A limited test release emphasizing safer runtime protections.

### Changed

- Narrowed `PlayerRagdoll4+` tag handling so defined tags are no longer rewritten before Unity reports an undefined tag.
- Reworked `NullRefGuard` so patches must provide a known-safe classifier before suppressing a null reference.
- Added config-gated behavior for client-side ragdoll destroy protection, entrance teleport update guarding, and EnemyAI NavMesh recovery.
- Changed `StartOfRound.RefreshPlayerVoicePlaybackObjects` handling to prefer vanilla execution and use the fallback only for known voice object failures.
- Changed `UnlockableSuit.Update` handling to let vanilla run when suit data is valid.
- Added assembly identity checks for sensitive version-specific behavior.
- Added lifecycle cache cleanup for particle mesh, warning limiter, voice playback, and network destroy guards.

## 0.0.1 - 2026-04-25

Initial public GitHub release.

### Added

- Runtime guards for known Lethal Company V81-era null-reference, index, audio, particle, NavMesh, and Netcode errors.
- Optional reflection-based compatibility guards for EnemyHealthBars, ShipLootPlus, ToggleableNightVision, and ChatCommands.
- Patch catalog documenting target methods, handled errors, trigger limits, and risk level.
