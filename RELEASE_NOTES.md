# RolCombat 1.0.0

First version distributed through GitHub Releases. Includes the combat rules from 0.1.8 without balance changes.

## Download and install

Download **Valmods.RolCombat-1.0.0.zip** from **Assets**, not the automatic “Source code” archives. In r2modman, open **Settings → Import local mod**, select the complete ZIP, and launch with **Start modded**. Requires BepInEx 5 for Valheim. Disable the previous version and avoid duplicate DLLs.

## Features

- **Tower shields:** 15% less incoming damage. Successful melee blocks apply Numbness: add 20% of the enemy's stagger threshold if it fits without triggering a stagger, and pause stagger recovery for 2 seconds.
- **Multiplayer tower blocking:** compensates for the multiplayer damage increase during frontal blocks with a tower shield.
- **Agility:** after 2 seconds with a one-handed weapon and no shield, dodging costs 25% less stamina and grants 0.10 seconds of additional invulnerability. Displays a buff with the Dodge skill icon.
- **Weapon blocking:** uses the one-handed weapon if its block power exceeds that of the non-shield off-hand item selected by the game.
- **Starred enemies:** 25% more damage per star compared with an unstarred enemy, replacing the original star damage bonus; 20% additional health while preserving the stagger threshold from before that health increase.

All players must use the same version and settings. Combat Recorder is a separate diagnostic tool and is not required to play.

**Validation:** builds and automated checks pass, and several features have been verified in single-player recordings. The additional enemy health and multiplayer blocking compensation still need in-game testing. The 1.0.0 version number does not imply that those features have been fully validated.
