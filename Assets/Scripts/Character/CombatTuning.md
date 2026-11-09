# Combat tuning

Select `Assets/Settings/CombatSettings.asset` to tune shared player/enemy attacks.
The player and StationaryEnemy reference it on their CombatAttack component.
Duplicate and assign that asset for an enemy with different timing or reach.
Damage and target layers remain on PlayerCombat / EnemyCombat.

- Player attacks: 2 x 2, 6 active frames, 8 recovery frames after the active period.
- Enemy attacks: 1.25 x 1.25, 18 windup frames, 6 active frames, 1-second cooldown.
- Buffered attack/parry inputs: 0.12 seconds. Directions are captured when attack is pressed.
- Frames mean seconds = frames / 60, independent of display refresh rate.
- Attack boxes start outside the body's collider bounds in the selected direction.
  Flipping or resizing the body updates placement automatically; clearance is 0.02 units.
- Terrain Layers blocks attacks and pogo through solid level geometry, including enemy weapons.

## Controls and outcomes

J attacks. W/Up + J attacks upward. S/Down + J attacks downward while airborne.
Space jumps. L starts a separate body parry: 10 active frames, 16 recovery frames,
and 30 frames of enemy stun on success. Parry timing is set on PlayerParry.
Attacking and parrying cannot be active together. A parry may cancel attack recovery.

Downward attacks pogo enemy bodies, active enemy weapons, and Pogoable objects.
There is one bounce per swing, with no normal jump spent or refilled.
Body hits deal damage once per enemy per swing; weapon pogo cancels that weapon swing.
Player damage protection prevents attack/contact damage from charging twice.
Respawn and death clear attacks, telegraphs, parries, and buffered inputs.

## Feedback and debugging

F1 toggles overhead stats and attack/parry boxes. The display includes box size,
active offset, recovery, parry phase, damage protection, and enemy stun.
Hiding debug visuals never disables combat collision.

## Silver spell and healing

On the player's **PlayerSpell** component, `Spell Color` is shared by the healing
circle and travelling crescent. `Projectile Size` is 0.55 x 0.95 world units (roughly the player's height);
it controls both the visual's full size and its swept damage box. The launch origin
keeps the box above the player's feet so grounded casts do not overlap the floor. The crescent
unfolds over `Projectile Unfold Seconds` (0.1), plays the existing grounded/air
sword swing, and inherits PlayerCombat's current damage, including shop upgrades.

Hold the cast/heal input for 1.5 seconds to heal once. The circle uses the larger
body dimension times `Healing Diameter Multiplier` (1.3), remains circular when
the player flips or is scaled, fades from transparent to opaque while focusing,
then fades out over `Heal Flash Seconds` (0.25). Damage or action cancellation
removes it. Both spell silhouettes are cached; travelling shots remain pooled.

## Platform encounters and boss contact

`VerticalPillar` enables **Enemy On Every Platform**: every climbing ledge,
including wide ledges and the approach to the boss, selects one enemy. The
starting floor and shop checkpoints stay safe; the arena hosts the boss. Existing
enemy unlock rows still introduce different enemy types gradually. Defeated
enemies stay defeated when their chunks reload. Disable the toggle to restore
the chance/row-spacing mode.

On `FireWormBossAnimated`, the root BoxCollider2D is 1.2 x 2.6 with Y offset -0.4;
the Tail collider is 4 x 0.85 with Y offset -0.1. The bottom edges retain their
previous positions, keeping artwork and gravity contact grounded. These are the
solid body/contact colliders; attack reach is configured separately in boss settings.
Enemy body color warns during windup; player color indicates active parry/protection.
Hit, parry, and pogo each have a small placeholder sound and a brief body flash.
Add CombatFeedback explicitly to a prefab to assign replacement clips or change its volume.
Set volume to zero to mute its cues. No external audio assets are required.

The death screen uses the main menu's dark gray background, white regular title,
and gray 320 x 60 buttons. Retry restarts the scene; Give Up quits (stops Play in Editor).
