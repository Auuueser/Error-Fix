# Changelog

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
