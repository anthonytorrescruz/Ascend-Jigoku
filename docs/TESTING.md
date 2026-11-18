# Acceptance testing

Use the final integrated standalone build. Record the exact UVCS changeset, Git commit, build filename, date, OS, controller, resolution and tester. For each item record Pass / Fail / Not run and a short evidence note. An unchecked item is not a claimed pass.

## Historical evidence

[November 17 controlled Play Mode report](verification-2026-11-17.txt): 85 checks passed for the then-current combat/boss implementation, including 10 connecting sword hits, parry follow-up protection, summons, arena movement and route persistence. Those checks used controlled scenarios and are **not** proof of a manually completed fight or the latest integrated ending. Later changes require fresh acceptance testing. No new tests were run for this review.

## Complete game flow

- [ ] Clean project import and zero compile errors; MainMenu is first in the build scene list.
- [ ] START and replayable TUTORIAL work; no debug menu is required to progress.
- [ ] Move, single/double jump, dash, directional attacks and repeatable pogo work.
- [ ] Tap spell casts from the ground and air; holding heals once per press; damage interrupts charging.
- [ ] Enemy contact and attacks damage once, apply horizontal knockback and preserve facing.
- [ ] Parry protects staggered follow-up attacks, permits a quick counterattack, then expires; a miss remains vulnerable.
- [ ] Fire Worm uses its intended attack/animation/audio patterns; victory unlocks checkpoint one.
- [ ] Checkpoint one heals, clears nearby threats and permits safe shop use.
- [ ] Valid upgrades cost currency; insufficient funds/maxed upgrades do not charge; upgrades persist correctly.
- [ ] Death, Retry, Give Up, pause/resume, quit and Continue behave correctly.
- [ ] Grim Death remains 10 HP; spells have readable warning/safe gaps; summons can be killed with melee/spell/pogo.
- [ ] Ending does not trigger before both bosses are defeated and checkpoint two is reached.
- [ ] At the ending, player cannot fall, take combat/fall/zone damage or be attacked; no monster sounds continue.
- [ ] Ending visuals, skip handling and end-card navigation work; returning to MainMenu restores normal menu behavior.
- [ ] Quitting during the cinematic retains a usable Continue; a fully completed run does not resurrect from backup.

## Reliability and presentation

- [ ] Keyboard-only and Xbox-only gameplay plus menu/shop/end-card navigation.
- [ ] Disconnect/reconnect controller during movement, charging and menus; switch input devices and inspect prompts.
- [ ] 1280x720, 1920x1080, a narrow/4:3 viewport and ultrawide: health bars, text and buttons remain legible/in bounds.
- [ ] Low frame rate and normal/high refresh: attacks, parry, pogo and telegraphs remain consistent.
- [ ] Master/music/SFX sliders and accessibility settings apply and persist; mute really mutes.
- [ ] Boss camera keeps the player visible; offscreen cues point to the correct side; normal framing returns after fights.
- [ ] Save/load from a clean launch, corrupt-primary/valid-backup recovery, missing save, interrupted write and New Run deletion. Back up real saves before destructive test cases.
- [ ] Normal release hides development-only hitboxes/debug UI, uses intended death-screen settings, and has no missing scripts/sprites/audio.
- [ ] Extract the release ZIP on a second computer without Unity; launch and complete the same workflow.

Known failures should be recorded openly with reproduction steps and scope. Do not mark this checklist complete merely because the Editor compiles or an earlier combat report passed.
