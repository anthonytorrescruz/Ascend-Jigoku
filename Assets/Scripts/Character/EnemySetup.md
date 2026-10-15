# Enemy variants

Drag a prefab from **Assets/Prefabs/Enemies/Variants** into a scene, or open
**Assets/Scenes/EnemyPlayground.unity** to try all four. Main is unchanged.

| Variant | Role | Health | Damage | Windup |
| --- | --- | --- | --- | --- |
| Fodder | Slow ground enemy; no evasions | 1 | 1 | 30 frames |
| Hopper | Chases and jumps to checked landings; can evade | 3 | 1 | 22 frames |
| Flyer | Flies toward the player; can strike down and evade | 2 | 1 | 26 frames |
| Brute | Slow, tougher, resists knockback | 6 | 2 | 42 frames |

All times marked as frames use 60 FPS. Contact damage starts at zero: touching
a body is safe; its telegraphed attack hurts. Raise Contact Damage if needed.

## Editing / adding a variant

1. Select its definition in **Assets/Settings/Enemies** and edit the Inspector.
   This changes every enemy using that asset. Restart Play to apply health/gravity.
2. For a new type, duplicate a definition and create a variant of an enemy prefab.
   Assign the new definition in **EnemyCombat > Definition**.
3. Keep EnemyBrain, EnemyMotor, EnemyCombat, EnemyHealth, CombatAttack, the solid
   Collider2D, and a Dynamic Rigidbody2D with only rotation frozen.

EnemyDefinition holds stats and behavior settings. EnemyBrain chooses patrol,
chase, evade and return. EnemyMotor handles physics, floor/wall checks, jumps,
and knockback. EnemyCombat uses the existing attack/parry/pogo resolver.
F1 shows AI state, health, velocity, attack state and the actual attack size.

Ground enemies stop at ledges. Hoppers only jump when they find a supported
landing and a clear arc; they cannot plan a route through a whole tower.
Flyers slide along nearby obstacles; they do not solve mazes. Put solid terrain
on the Ground layer, or update Terrain Layers in the definition and player
CombatSettings. Terrain triggers do not count as floors, walls or sight blockers.

Evasions watch visible, active player swings, wait Reaction Seconds, and then
respect Evade Cooldown. They do not read player inputs or redirect an attack
after its windup starts. Brutes and fodder do not evade by default.
Hits can interrupt attacks and push enemies away; Knockback Multiplier controls
resistance. Setting Hit Stun Seconds to zero prevents hit-stun, but parries still
stun all variants. The original StationaryEnemy has no definition and retains
its previous behavior. Existing spawners can use any of the new prefabs.
