# Enemy and player combat

Main now uses a single instance of `Assets/Prefabs/Enemies/StationaryEnemy.prefab`.
It combines the template's green triangle sprite with health, Pogoable, contact
damage, and stationary combat. The old template was below the camera at Y=-20.85;
the replacement is at (3.5, 0.32) on the visible ground. The empty Ground spawner
was removed. Drag the prefab into a scene to add another enemy.

## Controls

- J: attack in the facing direction.
- Hold W or Up Arrow and press J: attack upward, on the ground or in the air.
- Hold S or Down Arrow and press J: attack downward while airborne.
- Space: normal jump. W/Up Arrow aim attacks instead of jumping.

## Inspector settings

Select **Enemy** or open **StationaryEnemy.prefab** to change shared defaults.

| Component | Controls |
| --- | --- |
| Enemy Combat | Detection radius, detection interval, prediction frames, prediction distance cap, line of sight/obstacle layers, above threshold, windup frames, active frames, attack damage, side/up sizes and offsets, cooldown seconds, damage layers, target player. |
| Combat Attack | Parry window frames, attack visualization, whether enemy attack damage respawns the player. |
| Enemy Contact Damage | Body-contact damage, contact cooldown, whether body contact respawns the player. |
| Enemy Health | Maximum enemy health. |
| Player Combat | Damage, active/cooldown frames, side/up/down sizes and offsets, enemy damage layers. |
| Player Health | Protection duration after damage, preventing simultaneous attack/contact hits from charging twice. |

Frames are measured at a reference rate of **60 FPS**: duration = frames / 60.
This keeps timing consistent across display refresh rates. Defaults: enemy
prediction 8 frames, windup 8 frames, active attack 6 frames, and a full 1-second
cooldown after the active attack ends. Parry window defaults to 2 frames.

The enemy predicts constant horizontal momentum plus airborne gravity, capped
by Maximum Prediction Distance. It attacks left, right, or above when the
predicted player bounds intersect the chosen attack box. Line of sight uses the
configured obstacle layers. It does not chase or attack directly downward.

Yellow shows windup; red shows an active enemy strike; blue shows a player
strike; cyan briefly marks a parry. Disabling Show Attack changes visualization,
not hit detection. Attack visuals and trigger colliders are created once and
reused; overlaps reuse lists, targets are cached, and detection is throttled.

## Parries and pogo

Overlapping player/enemy strike boxes whose start times fall within the parry
window cancel both swings. Damage is buffered for this small window and resolved
after all combat updates, so script order cannot give one side an early hit.
Contact damage occurring during that parry is also suppressed. Strikes outside
the timing window behave normally.

Enemy strike hitboxes are Pogoable triggers. An airborne downward hit bounces the
player and cancels that strike without damaging the enemy through its weapon.
The enemy body and generic Pogoable colliders can still be pogoed repeatedly.
Pogo never spends or refills the normal jump budget. Body attacks still deal
configured attack damage, so an enemy can die after repeated hits; set Player
Combat > Attack Damage to 0 for indefinite pogo practice.

## Verification

59 Unity Play Mode checks passed, covering directions and controls, actual body
collision, prediction, wall occlusion, cooldown, frame timing, parries in both
orders and at timing boundaries, differing parry windows, body/weapon/generic
pogo, and jump resource conservation. Five additional live checks in Main
verified telegraphing, repeated attacks, cooldown, stationary physics, and damage.

Test reports, scene backups, and the rerunnable verification scripts are in:
`C:\Users\antho\Documents\Codex\2026-09-30\ch\work\enemy-combat`.
To rerun the checks, copy only EnemyCombatVerificationTemporary.cs and
EnemyCombatLiveCheckTemporary.cs into Assets/Editor, let Unity compile, then use
Tools > Verify Enemy Combat or Tools > Observe Enemy In Main. Both tools enter
Play Mode, perform checks, and stop; their test changes are not saved to Main.
