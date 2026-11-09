# RolCombat 1.0.1

Fixes stagger recovery for starred enemies with the additional health bonus. Their stagger threshold and recovery rate now both match the values from before that bonus.

For example, a one-star Fuling berserker keeps 1,920 health and a stagger threshold of 480, while stagger recovery returns from 115.2 to 96 points per second. Numbness still pauses recovery for 2 seconds and contributes 20% of the threshold when the increment fits without staggering.

No other balance changes. Player recovery and unstarred enemies are unaffected. This correction has been checked against the installed game's recovery code and offline tests; in-game multiplayer retesting is still needed.

Download **Valmods.RolCombat-1.0.1.zip** from **Assets** and import the complete ZIP in r2modman using **Settings → Import local mod**. Remove or disable older copies first. All players must use the same version and settings. Combat Recorder 0.1.7 remains compatible.

The previous 1.0.0 release and tag have been retired to avoid distributing the affected version.
