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
Enemy body color warns during windup; player color indicates active parry/protection.
Hit, parry, and pogo each have a small placeholder sound and a brief body flash.
Add CombatFeedback explicitly to a prefab to assign replacement clips or change its volume.
Set volume to zero to mute its cues. No external audio assets are required.

The death screen uses the main menu's dark gray background, white regular title,
and gray 320 x 60 buttons. Retry restarts the scene; Give Up quits (stops Play in Editor).
