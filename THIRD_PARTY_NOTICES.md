# Third-Party Notices

Error Fix includes optional compatibility guards for several third-party Lethal Company mods. These guards are applied through reflection when the relevant type is present at runtime.

No third-party mod source code, DLLs, Thunderstore package contents, or assets are included in this repository. The license entries below document upstream project metadata only and are not legal advice.

## Compatibility targets

| Project | Upstream | License / status |
| --- | --- | --- |
| EnemyHealthBars | https://thunderstore.io/c/lethal-company/p/NotezyTeam/EnemyHealthBars/ | No explicit license identified from the public Thunderstore/source metadata at the time this notice was prepared. |
| ShipLootPlus | https://github.com/ProfX66/ShipLootPlus | AGPL-3.0. Error Fix does not copy, modify, link against, or distribute ShipLootPlus source or binaries; it only detects public runtime types by reflection. |
| ToggleableNightVision | https://github.com/kennyhngo/LethalCompany-NightVision | No explicit license file identified in the linked public GitHub repository at the time this notice was prepared. |
| ChatCommands | https://github.com/Toemmsen96/ChatCommands | MIT. |

## Game and framework dependencies

This project references local Lethal Company, Unity, BepInEx, Harmony, and Netcode assemblies for compilation. Those files are not part of this repository and must be supplied by the user's local game/modding environment.
