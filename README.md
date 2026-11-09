# RolCombat

A combat mod for Valheim featuring tower shield bonuses, Agility when using a one-handed weapon without a shield, and adjustments to starred enemies.

## Download

Download **Valmods.RolCombat-1.0.0.zip** from the **Assets** section of [release 1.0.0](https://github.com/esPonders/combatrol/releases/tag/v1.0.0). You do not need the automatic “Source code” downloads.

## Install with r2modman

1. Select your Valheim profile and install BepInEx for Valheim if you have not already done so.
2. Open **Settings → Import local mod** and select the complete RolCombat ZIP without extracting it.
3. Disable or remove older versions of RolCombat or Tower Guard to avoid duplicate installations.
4. Launch the game with **Start modded**.

For multiplayer, everyone must use the same RolCombat version and settings. Install it on the server too if you use a dedicated server. Combat Recorder is not required to play.

## Manual installation

Requires BepInEx 5 for Valheim. Extract `plugins/RolCombat/Valmods.RolCombat.dll` from the ZIP and place it in `BepInEx/plugins/RolCombat/` inside your game installation or mod profile.

## Uninstall

Close Valheim and disable or uninstall RolCombat through r2modman. If you installed it manually, remove its DLL from `BepInEx/plugins/RolCombat/`.

The combat changes stop applying when the mod is removed. Enemy health is saved through the game's normal system, so injured creatures that were already saved may temporarily retain their previous health values.
