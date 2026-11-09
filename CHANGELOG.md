# RolCombat 1.0.3

Enemies cannot accumulate additional stagger damage while their stagger animation is active. Health damage and critical damage bonuses are unchanged; their existing stagger meter continues recovering normally and is not forcibly emptied.

Once the animation ends, attacks accumulate stagger again. This rule applies to wild enemies at all star levels; players and tamed creatures retain their normal behavior. Tower Numbness already rejects staggered enemies and does not pause recovery during this state.

Elite damage remains +20% per star. Elite stagger thresholds and recovery remain equivalent to the unstarred creature of the same type. Tower defense, Agility, and enemy health bonuses are unchanged.

Download **Valmods.RolCombat-1.0.3.zip** from **Assets** and import the complete ZIP using **Settings → Import local mod** in r2modman. Disable or remove older copies. All players and dedicated servers must update. Combat Recorder 0.1.7 remains compatible.

Validated with offline regression and assembly checks. In-game validation is still needed. The preceding release is retired after successful publication.

# RolCombat 1.0.2

Starred enemies now deal +20% damage per star: 120% for one star and 140% for two stars, replacing the vanilla star damage bonus.

Their stagger threshold and recovery rate now match an unstarred creature of the same type. A Fuling berserker uses a threshold of 240 and recovers 48 stagger points per second at every star level. Starred enemies keep their additional health, including the mod's 20% health bonus.

Tower blocks still contribute 20% of the threshold only when the complete increment fits without staggering the enemy. Numbness pauses recovery for 2 seconds. Tower damage reduction and Agility are unchanged.

Existing profiles using the previous 25% damage default migrate to 20% on first launch; other custom values are preserved. All players and dedicated servers must use the same version and settings.

Download **Valmods.RolCombat-1.0.2.zip** from **Assets**, then import the complete ZIP through **Settings → Import local mod** in r2modman. Disable or remove older copies. Combat Recorder 0.1.7 remains compatible.

Verified with offline rules and assembly checks; in-game testing is still needed. The preceding release is retired after this package is published.

# Changelog

## 1.0.1
- Preserve original stagger recovery speed when increasing elite health.
- Keep original stagger threshold, Numbness behavior, and other combat rules.
- Retire the 1.0.0 release and tag.

## 1.0.0
- Primera distribución mediante GitHub Releases, con ZIP importable en r2modman.
- Incluye las reglas de 0.1.8 sin cambios de equilibrio.
- Se mantienen documentadas las pruebas pendientes de vida de élites y multijugador.

## 0.1.8
- Élites con 20 % adicional de vida, conservando el umbral anterior de desequilibrio.
- Reconciliación de vida al cargar desde base y nivel para evitar aumentos acumulativos.

## 0.1.7
- Compensación del aumento de daño multijugador antes del cálculo de bloqueo con torre.

## 0.1.6
- Compatibilidad con el parámetro Int16 añadido a AddStatusEffect en Valheim 1.0.

## 0.1.5
- Nuevo nombre: RolCombat; se conserva el identificador técnico de Tower Guard.
- Buff Agilidad con icono de esquiva y espera continua de 2 segundos.

## 0.1.4
- Esquiva sin escudo y con arma de una mano: 25 % menos de vigor y 0,10 s extra de invulnerabilidad.
- Selección del arma para bloquear si supera al objeto secundario que no sea escudo.

## 0.1.3
- Descripción breve en torre: Entumecimiento: 2s.

## 0.1.2
- Pausa de recuperación de desequilibrio durante 2 segundos tras bloqueo válido.
- Activación automática al arrancar.

## 0.1.1
- Corrección del acceso a equipo y diagnóstico de suspensión por errores.

## 0.1.0
- Tower Guard inicial: +25 % daño por estrella, reducción del 15 % con torre y preparación del 20 % de desequilibrio por bloqueo sin aturdir.
