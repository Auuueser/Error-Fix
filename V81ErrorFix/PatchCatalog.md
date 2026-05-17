# V81 Error Fix Patch Catalog

This file is a maintenance index for the active Harmony patches. It documents what each patch is allowed to touch so broad guards stay narrow over time.

## Global Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `DisabledAudioSourcePlayGuardPatch` | `AudioSource.Play*` | `Can not play a disabled audio source` | Only blocks playback when the source exists and `isActiveAndEnabled == false`; original playback is allowed if the guard fails. | Medium, global Unity audio API patch. |
| `AudioSourcePlayOneShotNullClipGuardPatch` | `AudioSource.PlayOneShot` | `PlayOneShot was called with a null AudioClip` | Only blocks `PlayOneShot` when the clip argument is null. | Low. |
| `UnityKnownWarningFilterPatch` | `BepInEx.Logging.UnityLogSource.OnUnityLogMessageReceived` | Known Unity warning spam for missing audio spatializer plugin setup, BoxCollider negative scale/size, SteamValve empty AudioSource, and duplicate Static Lighting Sky warnings | Enabled by default as a log-only filter; filters only exact Unity warning message prefixes and only for `LogType.Warning`; Netcode lifecycle warnings are not filtered. | Low, global log callback patch. |
| `PlayerRagdollCompareTagGuardPatch` | `GameObject.CompareTag`, `Component.CompareTag` | Undefined `PlayerRagdoll4+` tag comparisons | Only suppresses Unity undefined-tag exceptions for numeric `PlayerRagdoll` tags. | Medium, global tag comparison patch. |
| `GameObjectPlayerRagdollTagSetterGuardPatch` | `GameObject.tag` setter | Undefined `PlayerRagdoll4+` tag assignments | Never rewrites defined tags in advance; only falls back to `PlayerRagdoll` after Unity throws the known undefined numeric ragdoll tag exception. | Medium, global tag setter patch. |
| `PlayerRagdollFind*TagGuardPatch` | `GameObject.FindWithTag`, `FindGameObjectWithTag`, `FindGameObjectsWithTag` | Undefined `PlayerRagdoll4+` tag lookups during ragdoll/player cleanup | Only handles numeric `PlayerRagdoll` tags; returns null or an empty array for missing lookup results. | Medium, global tag lookup patch. |
| `NetworkObjectDestroyGuardPatch` | `UnityEngine.Object.Destroy` | Client-side destroy of spawned `RagdollGrabbableObject` network objects | Config-gated; only blocks non-server destruction of spawned ragdoll grabbable network objects and allows lifecycle destroys during shutdown, ship unload, and lobby transitions. | Medium, global destroy patch. |
| `NetworkObjectParentChangedPatch` | `NetworkObject.OnTransformParentChanged` | Netcode reparent `SpawnStateException` if the runtime throws instead of logging internally | Config-gated; does not skip vanilla parent handling and only returns a thrown known spawned-state exception if one escapes the Netcode method. | Low. |
| `SoundManagerPlayAmbienceClipLocalPatch` | `SoundManager.PlayAmbienceClipLocal` | Ambience clip index out of range | Validates `soundType` and `clipIndex` before local ambience playback; suppresses only known index exceptions. | Low. |

## NavMesh Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `NavMeshSurfaceCollectSourcesPatch` | `NavMeshSurface.CollectSources` | Runtime NavMesh build errors from unreadable mesh sources | Only removes unreadable mesh build sources; all other sources remain unchanged. | Low. |
| `EnemyAINavMeshGuardPatch` | `EnemyAI.DoAIInterval` | `Agent not on nav mesh when trying to set destination` | Config-gated; only runs when an enemy has a moving, enabled `NavMeshAgent` that is off the NavMesh. Non-authority clients are suppressed; host/server owners optionally try nearby warp within the configured radius. | Medium. |

## Particle And Mesh Guards

| Component | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `ParticleMeshShapeGuard` | Particle systems with mesh shape modules | Unreadable mesh and zero-area mesh particle shape warnings | Disabled by default; scans once after scene load instead of periodically, uses per-frame batch and time budgets with resumable mesh inspection, skips expensive full-area scans for large meshes, reuses scratch lists, supports dry-run logging, clears scene caches on unload, caches mesh inspection results by mesh reference, and caps batched warning growth. | Low. |

## RPC And Item Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `HUDManagerAddChatMessagePatch` / `HUDManagerAddTextMessageClientRpcPatch` | HUD chat message methods | Chat RPC null references during scene/network sync | Suppresses known null references only; `ClientRpc` prefix and finalizer suppress only in generated Execute stage and never block send stage. | Low. |
| `HUDManagerSyncAllPlayerLevelsServerRpcPatch` | `HUDManager.SyncAllPlayerLevelsServerRpc()` | Non-owner client invoking owner-required level sync ServerRpc after ship unlockable sync | Skips the call only when the local client does not own `HUDManager`; the server call would be rejected by Netcode anyway. | Low. |
| `InteractTriggerUpdateUsedByPlayerClientRpcPatch` | `InteractTrigger.UpdateUsedByPlayerClientRpc` | Interact trigger RPC null references | Suppresses known null references only in generated Execute stage and never blocks send stage. | Low. |
| `RadMechAISetExplosionPatch` / `RadMechAISetExplosionClientRpcPatch` | RadMech explosion methods | Explosion RPC null references | Suppresses known null references only; the `ClientRpc` finalizer suppresses only in generated Execute stage. | Low. |
| `JetpackItemDeactivateJetpackPatch` / `JetpackItemItemActivatePatch` / `GrabbableObjectActivateItemRpcPatch` | Jetpack activation/deactivation RPC path | Jetpack null references wrapped as RPC exceptions | Only suppresses known null references; `GrabbableObject.ActivateItemRpc` is limited to `JetpackItem`. | Low. |
| `PlayerControllerBThrowObjectClientRpcPatch` | `PlayerControllerB.ThrowObjectClientRpc` | Held object mismatch/null RPC error | Lets normal Execute-stage paths run vanilla; replaces only known unresolved-object, missing component, missing item properties, unsafe drop dependency, or held-object mismatch cases. | Low. |
| `PlayerControllerBPlayerJumpedClientRpcPatch` | `PlayerControllerB.PlayerJumpedClientRpc` | Jump RPC null/out-of-range exceptions from missing jump dependencies | Suppresses only known dependency exceptions in generated Execute stage; unknown exceptions are logged and returned. | Low. |
| `PlayerControllerBGrabObjectClientRpcPatch` | `PlayerControllerB.GrabObjectClientRpc` | Grab-object RPC null references during player/object desync | Suppresses only known unresolved-object, missing `GrabbableObject`, held-object, reparent, audio, and tutorial dependency null references in generated Execute stage; unknown null references are logged and returned. | Low. |
| `SandSpiderAIPlayerLeaveWebClientRpcPatch` | `SandSpiderAI.PlayerLeaveWebClientRpc` | Spider web trap list index out of range during player/trap desync | Suppresses only known out-of-range exceptions in generated Execute stage. | Low. |
| `StartOfRoundRefreshPlayerVoicePlaybackObjectsPatch` | `StartOfRound.RefreshPlayerVoicePlaybackObjects` | Voice playback binding null references and missing voice components | Lets vanilla run by default; uses fallback only for known incomplete voice objects or vanilla NRE, and clears cache on scene/player changes. | Medium. |

## Gameplay Object Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `DocileLocustBeesAIUpdatePatch` | `DocileLocustBeesAI.Update` | Missing bee update dependencies | Skips only frames where required references are missing. | Low. |
| `CrawlerAIUpdatePatch` | `CrawlerAI.Update` | Missing crawler update dependencies | Skips only frames where required references are missing; clears target movement on suppressed null reference. | Low. |
| `TerminalAccessibleObjectUpdatePatch` | `TerminalAccessibleObject.Update` | Missing terminal/radar dependencies | Skips frames until dependencies are ready and tries normal initialization when possible. | Low. |
| `UnlockableSuitUpdatePatch` | `UnlockableSuit.Update` | Suit ID out of range | Lets vanilla run when `syncedSuitID` and dependencies are valid; skips only invalid IDs or missing renderer/sync state. | Low. |
| `StartOfRoundSpawnUnlockableSuitNetworkVariablePatch` / `StartOfRoundSyncShipUnlockablesClientRpcSuitGuardPatch` / `UnlockableSuitSwitchSuit*Patch` | Ship unlockable and suit sync | Pre-spawn suit `NetworkVariable` warning, invalid suit IDs, and malformed suit/unlockable sync payloads | Rewrites only the verified `UnlockableSuit.syncedSuitID.Value` initialization in `StartOfRound.SpawnUnlockable` to Netcode `Reset` before spawn; `ClientRpc` guard runs only in Execute stage; replaces only invalid player suit IDs with suit 0 when that fallback exists; unknown suit-switch exceptions are returned. | Medium. |
| `SteamValveDamageTriggerSpawnPatch` | `NetworkObject.InvokeBehaviourNetworkSpawn` | Netcode skipping inactive SteamValve `damageTrigger` `InteractTrigger` during spawn | Disabled by default because this warning is usually one-time spawn lifecycle noise, not a performance issue. Only `Enabled` installs the experimental guard; `Auto` is treated as disabled. If enabled, it only temporarily activates inactive child `damageTrigger` under `SteamValveHazard` while Netcode runs spawn callbacks, then restores it inactive. | Low. |
| `DeadBodyInfoPlayerRagdollTagGuardPatch` | `DeadBodyInfo.Start` | Player ragdoll player index/suit index out of range and undefined ragdoll tags | Suppresses known out-of-range/tag errors and applies fallback ragdoll tags. | Medium. |
| `QuicksandTrigger` patches | Quicksand trigger enter/exit path | Trigger exit null references | Suppresses known null references only. | Low. |
| `EntranceTeleport` patches | Entrance teleport update/path lookup | Missing entrance/main entrance references | `EntranceTeleport.Update` guard is config-gated and defaults to verified Assembly-CSharp only; it preserves the original hover tip and skips only missing exit/trigger frames. | Low. |
| `GrabbableObjectPhysicsTriggerOnTriggerEnterPatch` / `SoccerBallProp*Patch` | Soccer ball physics trigger and kick path | Soccer ball kick null references from missing round, player, ship, or collider references | Skips only the unsafe trigger/kick frame when required references are missing; finalizers suppress only the same known-safe dependency failures and return unknown null references. | Low. |

## Optional Mod Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `EnemyHealthBarsLateUpdatePatch` | `EnemyHealthBars.Scripts.HealthBar.LateUpdate` | EnemyHealthBars null references | Optional reflection patch with an independent config switch; only suppresses known destroyed/missing health-bar layout references when the expected member exists. | Low. |
| `ShipLootPlusUiHelperPatch` | `ShipLootPlus.Utils.UiHelper.CalculateLootValue`, `RefreshElementValues`, generated loot-value lambdas, `UpdateDatapoints.MoveNext` | ShipLootPlus loot UI null references during ship unlockable sync and grab refresh | Optional reflection patch with an independent config switch; validates expected method/member signatures, caches reflection lookups, throttles held-scrap scene scans, supports known `CalculateLootValue` ignored-list signatures, and only suppresses known missing UI/datapoint or held-scrap owner references. | Low. |
| `LobbySlotSetModdedIconPatch` | `LobbySlot.SetModdedIcon` | Missing lobby modded-state icon references with lobby list mods | Checks required icon fields before toggling them; suppresses only known null references. | Low. |
| `NightVisionInsideLightingPostfixPatch` | `NightVision.Patches.NightVisionOutdoors.InsideLightingPostfix` | NightVision outdoor lighting null references during teleport/inside lighting updates | Optional reflection patch with an independent config switch; only suppresses known missing `TimeOfDay.sunIndirect` or HDRP light component references. | Low. |
| `ChatCommandApiStartHostPostfixPatch` | `ChatCommandAPI.Patches.GameNetworkManager_StartHost.Postfix` | ChatCommandAPI null reference after pressing host/confirm host | Optional reflection patch with an independent config switch; only suppresses known null confirmation request storage when the expected member exists. | Low. |

## Maintenance Rules

- Keep global patches narrow and return the original exception for unknown errors.
- Use `WarningLimiter` for new warnings; default warning cap is 5.
- Unknown optional-mod null references must be logged and returned to the original caller.
- Do not copy build output to external folders; build output remains under `build_tmpbin`.
- Bump only `VersionInfo.PluginVersion` for maintenance releases.
