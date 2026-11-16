# Grim Death

For the presentation, open **Tools > Anthony > Open Grim Death Practice**, then press Play.
The same encounter is part of **ProceduralPillar**, the gameplay scene launched by MainMenu:
Fire Worm -> first checkpoint (200) -> Grim Death -> second checkpoint (400).

Grim Death has **25 health**, following the last requested value. A connecting player hit
removes one boss health, regardless of weapon upgrades. Every boss attack removes one
player health and uses the existing damage protection and horizontal knockback.

## Fight

- Landing on the arena starts the boss bar and supplied battle music. Approach to wake it.
- **Scythe combo:** two separately timed cuts; parry with L/LB, retreat, or pogo.
- **Reaping dash:** a warned, fast charge with a brief phased body, followed by recovery.
- **Shadow step:** a violet destination rune appears before a short blink behind the player.
  The body returns only at a clear location and becomes vulnerable again immediately.
- **Soul rain:** a fan of ballistic shadow bolts aimed at the player's position when the cast starts.
- **Shadow pillars:** equally spaced, tall columns. Walk or dash into the gaps; don't try to jump over them.
- **Eclipse:** a circular warning gives time to leave the blast radius.
- **Soul call:** up to three one-health spirits. They appear, telegraph a locked lunge,
  and can be killed with melee, pogo strikes, or ranged spells before playing their death animation.
  The summon cap and cooldown prevent flooding.

All six attacks and summons are available from the start. There is no fire or slime phase.
Parry cancels the connecting attack and stuns the boss; pogo cancels an enemy weapon or bolt
without consuming a jump. Damage does not erase an already advertised boss attack.
The brief dash/blink invulnerability is limited to transit; recovery and parry stun are openings.

## Tuning

Select **Assets/Settings/GrimDeath.asset** to change health, name, movement speed,
shadow-step timing, scythe frames, dash speed, warning times, spell sizes, pillar spacing,
summon cap/cooldown, spirit hover height and hurtbox size/offset, animation rate and audio volumes. Times are seconds unless marked as
frames at 60 FPS. The boss prefab and spirit prefab are in Assets/Prefabs/Enemies.

The whole arena width comes from VerticalPillar's pillarWidth; no central combat subsection
is used. The next checkpoint remains sealed until the supplied death animation completes.
Both boss victories are saved separately. Old saves from above the new boss resume at the
previous checkpoint so they cannot accidentally bypass it. Existing weapon/upgrades remain intact.

## Assets and efficiency

All nine PNG animation sheets and all four Aseprite source documents from the supplied
Undead Executioner ZIP are retained. Packing blanks are excluded from idle/attack/summon;
intentional transparent death frames are retained. Source cell pivots stay stable while playing.

Art: Undead Executioner by Kronovi-, https://darkpixel-kronovi.itch.io/undead-executioner
Music: user-supplied audio_keeper-boss-battle-500204.mp3, imported unchanged and streamed.
Nine original, baked WAV effects cover scythe, blink, casting, spirits, hit, death, pillars and burst.
No runtime audio synthesis or external sound downloads are required.

Spell objects are pooled with a hard cap of 64; spirit population is capped separately.
Shared warning shapes/materials and reused physics-query lists avoid per-frame creation.
The boss and its floor stay loaded while unbeaten, preserving HP during streaming.
The boss HUD is created only at runtime, scales with the game's UI canvas, and never blocks input.
