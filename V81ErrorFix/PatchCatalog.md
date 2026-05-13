# V81 Error Fix Patch Catalog

This file is a maintenance index for the active Harmony patches. It documents what each patch is allowed to touch so broad guards stay narrow over time.

## Global Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `DisabledAudioSourcePlayGuardPatch` | `AudioSource.Play*` | `Can not play a disabled audio source` | Only blocks playback when the source exists and `isActiveAndEnabled == false`; original playback is allowed if the guard fails. | Medium, global Unity audio API patch. |
| `AudioSourcePlayOneShotNullClipGuardPatch` | `AudioSource.PlayOneShot` | `PlayOneShot was called with a null AudioClip` | Only blocks `PlayOneShot` when the clip argument is null. | Low. |
| `PlayerRagdollCompareTagGuardPatch` | `GameObject.CompareTag`, `Component.CompareTag` | Undefined `PlayerRagdoll4+` tag comparisons | Only suppresses Unity undefined-tag exceptions for numeric `PlayerRagdoll` tags. | Medium, global tag comparison patch. |
| `GameObjectPlayerRagdollTagSetterGuardPatch` | `GameObject.tag` setter | Undefined `PlayerRagdoll4+` tag assignments | Never rewrites defined tags in advance; only falls back to `PlayerRagdoll` after Unity throws the known undefined numeric ragdoll tag exception. | Medium, global tag setter patch. |
| `PlayerRagdollFind*TagGuardPatch` | `GameObject.FindWithTag`, `FindGameObjectWithTag`, `FindGameObjectsWithTag` | Undefined `PlayerRagdoll4+` tag lookups during ragdoll/player cleanup | Only handles numeric `PlayerRagdoll` tags; returns null or an empty array for missing lookup results. | Medium, global tag lookup patch. |
| `NetworkObjectDestroyGuardPatch` | `UnityEngine.Object.Destroy` | Client-side destroy of spawned `RagdollGrabbableObject` network objects | Config-gated; only blocks non-server destruction of spawned ragdoll grabbable network objects and allows lifecycle destroys during shutdown, ship unload, and lobby transitions. | Medium, global destroy patch. |
| `NetworkObjectParentChangedPatch` | `NetworkObject.OnTransformParentChanged` | Netcode reparent `SpawnStateException` while despawning/unloading unspawned objects | Skips parent-change handling only when the `NetworkObject` is not spawned, and suppresses only the known spawned-state exception. | Medium, global Netcode object patch. |
| `SoundManagerPlayAmbienceClipLocalPatch` | `SoundManager.PlayAmbienceClipLocal` | Ambience clip index out of range | Validates `soundType` and `clipIndex` before local ambience playback; suppresses only known index exceptions. | Low. |

## NavMesh Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `NavMeshSurfaceCollectSourcesPatch` | `NavMeshSurface.CollectSources` | Runtime NavMesh build errors from unreadable mesh sources | Only removes unreadable mesh build sources; all other sources remain unchanged. | Low. |
| `EnemyAINavMeshGuardPatch` | `EnemyAI.DoAIInterval` | `Agent not on nav mesh when trying to set destination` | Config-gated; only runs when an enemy has a moving, enabled `NavMeshAgent` that is off the NavMesh. Non-authority clients are suppressed; host/server owners optionally try nearby warp within the configured radius. | Medium. |

## Particle And Mesh Guards

| Component | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `ParticleMeshShapeGuard` | Particle systems with mesh shape modules | Unreadable mesh and zero-area mesh particle shape warnings | Scans in batches after scene load, clears scene caches on unload, caches mesh inspection results by mesh reference, disables only invalid mesh shape modules, and batches warnings. | Low. |

## RPC And Item Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `HUDManagerAddChatMessagePatch` / `HUDManagerAddTextMessageClientRpcPatch` | HUD chat message methods | Chat RPC null references during scene/network sync | Suppresses known null references only. | Low. |
| `HUDManagerSyncAllPlayerLevelsServerRpcPatch` | `HUDManager.SyncAllPlayerLevelsServerRpc()` | Non-owner client invoking owner-required level sync ServerRpc after ship unlockable sync | Skips the call only when the local client does not own `HUDManager`; the server call would be rejected by Netcode anyway. | Low. |
| `InteractTriggerUpdateUsedByPlayerClientRpcPatch` | `InteractTrigger.UpdateUsedByPlayerClientRpc` | Interact trigger RPC null references | Suppresses known null references only. | Low. |
| `RadMechAISetExplosionPatch` / `RadMechAISetExplosionClientRpcPatch` | RadMech explosion methods | Explosion RPC null references | Suppresses known null references only. | Low. |
| `JetpackItemDeactivateJetpackPatch` / `JetpackItemItemActivatePatch` / `GrabbableObjectActivateItemRpcPatch` | Jetpack activation/deactivation RPC path | Jetpack null references wrapped as RPC exceptions | Only suppresses known null references; `GrabbableObject.ActivateItemRpc` is limited to `JetpackItem`. | Low. |
| `PlayerControllerBThrowObjectClientRpcPatch` | `PlayerControllerB.ThrowObjectClientRpc` | Held object mismatch/null RPC error | Suppresses only the known mismatch/null case. | Low. |
| `PlayerControllerBGrabObjectClientRpcPatch` | `PlayerControllerB.GrabObjectClientRpc` | Grab-object RPC null references during player/object desync | Suppresses only known null references. | Low. |
| `SandSpiderAIPlayerLeaveWebClientRpcPatch` | `SandSpiderAI.PlayerLeaveWebClientRpc` | Spider web trap list index out of range during player/trap desync | Suppresses only known out-of-range exceptions. | Low. |
| `StartOfRoundRefreshPlayerVoicePlaybackObjectsPatch` | `StartOfRound.RefreshPlayerVoicePlaybackObjects` | Voice playback binding null references and missing voice components | Lets vanilla run by default; uses fallback only for known incomplete voice objects or vanilla NRE, and clears cache on scene/player changes. | Medium. |

## Gameplay Object Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `DocileLocustBeesAIUpdatePatch` | `DocileLocustBeesAI.Update` | Missing bee update dependencies | Skips only frames where required references are missing. | Low. |
| `CrawlerAIUpdatePatch` | `CrawlerAI.Update` | Missing crawler update dependencies | Skips only frames where required references are missing; clears target movement on suppressed null reference. | Low. |
| `TerminalAccessibleObjectUpdatePatch` | `TerminalAccessibleObject.Update` | Missing terminal/radar dependencies | Skips frames until dependencies are ready and tries normal initialization when possible. | Low. |
| `UnlockableSuitUpdatePatch` | `UnlockableSuit.Update` | Suit ID out of range | Lets vanilla run when `syncedSuitID` and dependencies are valid; skips only invalid IDs or missing renderer/sync state. | Low. |
| `StartOfRoundSyncShipUnlockablesClientRpcSuitGuardPatch` / `UnlockableSuitSwitchSuit*Patch` | Ship unlockable and suit sync | Invalid or short suit/unlockable sync arrays, invalid suit IDs | Sanitizes incoming arrays and skips invalid suit switches before the base method indexes unlockables. | Medium. |
| `DeadBodyInfoPlayerRagdollTagGuardPatch` | `DeadBodyInfo.Start` | Player ragdoll player index/suit index out of range and undefined ragdoll tags | Suppresses known out-of-range/tag errors and applies fallback ragdoll tags. | Medium. |
| `QuicksandTrigger` patches | Quicksand trigger enter/exit path | Trigger exit null references | Suppresses known null references only. | Low. |
| `EntranceTeleport` patches | Entrance teleport update/path lookup | Missing entrance/main entrance references | `EntranceTeleport.Update` guard is config-gated and defaults to verified Assembly-CSharp only; it preserves the original hover tip and skips only missing exit/trigger frames. | Low. |
| `GrabbableObjectPhysicsTriggerOnTriggerEnterPatch` / `SoccerBallProp*Patch` | Soccer ball physics trigger and kick path | Soccer ball kick null references from missing round, player, ship, or collider references | Skips only the unsafe trigger/kick frame when required references are missing; otherwise the vanilla kick logic runs. | Low. |

## Optional Mod Guards

| Patch | Target | Fixes | Trigger limit | Risk |
| --- | --- | --- | --- | --- |
| `EnemyHealthBarsLateUpdatePatch` | `EnemyHealthBars.Scripts.HealthBar.LateUpdate` | EnemyHealthBars null references | Optional reflection patch; only applies if the mod type and expected method signature exist. | Low. |
| `ShipLootPlusUiHelperPatch` | `ShipLootPlus.Utils.UiHelper.CalculateLootValue`, `RefreshElementValues`, generated loot-value lambdas, `UpdateDatapoints.MoveNext` | ShipLootPlus loot UI null references during ship unlockable sync and grab refresh | Optional reflection patch; validates expected method signatures and only suppresses known null references while refreshing loot UI. | Low. |
| `LobbySlotSetModdedIconPatch` | `LobbySlot.SetModdedIcon` | Missing lobby modded-state icon references with lobby list mods | Checks required icon fields before toggling them; suppresses only known null references. | Low. |
| `NightVisionInsideLightingPostfixPatch` | `NightVision.Patches.NightVisionOutdoors.InsideLightingPostfix` | NightVision outdoor lighting null references during teleport/inside lighting updates | Optional reflection patch; only applies if the NightVision type and expected method signature exist, and only suppresses known null references. | Low. |
| `ChatCommandApiStartHostPostfixPatch` | `ChatCommandAPI.Patches.GameNetworkManager_StartHost.Postfix` | ChatCommandAPI null reference after pressing host/confirm host | Optional reflection patch; only applies if the ChatCommandAPI patch type and expected method signature exist, and only suppresses known null references. | Low. |

## Maintenance Rules

- Keep global patches narrow and return the original exception for unknown errors.
- Use `WarningLimiter` for new warnings; default warning cap is 5.
- Do not copy build output to external folders; build output remains under `build_tmpbin`.
- Bump only `VersionInfo.PluginVersion` for maintenance releases.
