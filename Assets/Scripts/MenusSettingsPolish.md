# Menus, settings, saved runs, encounter polish, environment (AJ-60, AJ-66, AJ-75, AJ-77, AJ-78)

Everything here is added at runtime, so no scene files were edited. Auto-installers skip scenes that already contain the component, so you can add a component by hand when you want to tune it in the Inspector.

## Pause menu (AJ-66)
- `UI/PauseMenu.cs`. Esc or Start pauses. The menu has Resume, Settings, Main Menu and Quit. Esc, Start or B resumes.
- It's installed automatically in any scene with a `PlayerHealth`. AnthonyScene is skipped because it has its own test pause.
- It won't open while the shop is open, during the shop's input release, or after death.
- Gameplay starts and resumes at the Game speed setting.

## Consistent styling (AJ-66)
- `UI/MenuStyle.cs` is the single source for menu colors, sizes and fonts. The values were copied from `MainMenu.unity`: 1280x720 scaler, 320x60 gray tint buttons, legacy font, white text.
- The pause menu, the settings screen and the checkpoint shop all use it. In the shop, only the colors changed; the layout is the same.
- When the UI direction is decided, restyle in `MenuStyle` only.
- `UI/MainMenuExtras.cs` adds SETTINGS between START and QUIT by copying the scene's Quit button. It also adds CONTINUE when a run is saved (see AJ-75). Hand-authored buttons named "Settings Button" or "Continue Button" turn the matching part off.

## Settings (AJ-60 audio, keybinds, AJ-78 accessibility)
- `Settings/GameSettings.cs` stores the settings in PlayerPrefs.
  - Audio: Master (AudioListener), Music and SFX.
  - SFX is applied in `CombatFeedback.PlayClip`, which almost every sound uses, plus the Fire Worm presentation and the shop whisper.
  - Music currently covers the boss theme only, because no other music is wired yet. `MainMusic.mp3` and `ambientDS.mp3` are unused.
- `Settings/KeybindSettings.cs` handles keyboard rebinding for Move up/down/left/right, Jump, Attack, Dash, Parry, Shoot/Heal and Interact.
  - Binding a key that's already in use swaps the two actions.
  - Arrow keys stay as fixed movement alternates. Esc and F1 are reserved. The gamepad layout is fixed.
  - Any script that creates `new PlayerActions()` must call `KeybindSettings.Register(controls.asset)` right after. The four player scripts and ActiveInputPrompts already do.
  - The shop's interact action now comes from `KeybindSettings.CreateInteractAction`.
- `UI/SettingsMenu.cs` has three tabs (Audio, Controls, Accessibility) and is opened from both menus.

## Saved runs (AJ-75)
- `Gameplay/RunSave.cs` is a single save slot, stored as `run-save.json` in `Application.persistentDataPath`.
- It saves automatically:
  - when a run starts
  - when a checkpoint is activated
  - after each shop purchase (the merchants stand on checkpoints)
  - when the boss is defeated
- What it stores: seed, checkpoint, boss defeated, upgrades bought, and coins as of that save.
- Anything after the last save is not kept. Death (Retry) and CONTINUE both return you to the last save. This changes the old shop behavior, where upgrades and coins reset on every reload.
- Main menu: CONTINUE appears when a save exists. START becomes NEW RUN and needs a second click, because it deletes the save.
- `PlayerUpgrades` records how much each upgrade granted, so a fresh player can be re-granted the same stats. Wallets restore their balance through `CurrencyWallet.TrySetBalance`; the real economy wallet should override it.
- Editor menu: Tools > Ascend Jigoku > Saved Run (Delete / Show Save File / Log Save Contents).

## Encounter polish (AJ-78)
- `Gameplay/EncounterFeedback.cs` adds screen shake, hit-stop, a red damage and low-health vignette (in line with the minimal-HUD plan), and enemy death bursts.
  - These are hooked from `CombatFeedback.Hit`, `Parry` and `Pogo`. Tuning constants are at the top of the file.
- `Gameplay/TelegraphMarker.cs` shows a "!" over an enemy during its windup when Stronger attack warnings is on.
- Accessibility options: screen shake 0–100%, game speed 50–100%, hit-stop, reduce flashing (replaces the 60 Hz invulnerability flicker and white hit flash with steady tints), stronger warnings and the vignette.

## Environment (AJ-77)
- `Terrain/PillarEnvironment.cs` is added next to `VerticalLevelGenerator`.
  - Four height zones change the sky color and tint generated rock. The generator calls `PillarEnvironment.TintRock`; the boss floor and marker colors are unchanged.
  - Two seeded parallax layers of distant pillars and ambient embers.
  - A zone title fades in the first time the player climbs into each zone.
- Zone names and colors are placeholders, editable on the component.
- The backdrops are generated textures, so authored art can replace them later.

## Pixel-art terrain
- `Terrain/TerrainSkin.cs` paints every generated platform and rock body at 32 px/unit, the same density as the player sprites.
- The style is deliberately simple to match the character and parallax: about five flat colours per platform, shadows shifted toward navy, a bold lit top, a chunky shadow band, a few slab seams, and the occasional glow vein (zone colour; cyan on checkpoints).
- The look is deterministic from the run seed and row. Textures are freed when chunks unload.
- Colliders are unchanged: the skin is a child sprite, and the flat renderer is disabled.
- Turn it off with PillarEnvironment > Pixel Art Terrain.
- It's a stand-in until pre-made blocks are imported. At that point, swap TerrainSkin for a tiled/9-sliced sprite on the platform prefab.

## Debug actions (F1)
- F1 now also shows a clickable DEBUG ACTIONS panel at the bottom left. Number keys still work.
  1. Take damage
  2. Respawn
  3. Restore health
  4. Teleport to next checkpoint
  5. Teleport to previous checkpoint
  6. Teleport up one chunk
  7. +500 coins
  8. Kill enemies nearby
  9. Defeat boss / open route
- Teleports stop at the boss arena while the boss is alive. Results go to the Console with a `[Debug]` prefix.
- Editor and development builds only.

## Changed team files (small, marked edits)
CombatFeedback, CameraFollow (shake offset), BossEncounterMusic, FireWormPresentation, ShopMenu, ActiveInputPrompts, PlayerControlLock, PlayerController/Spell/Parry/Combat (one Register line each), VerticalLevelGenerator (zone tint, run save/restore), PlayerUpgrades, CurrencyWallet, PrototypeWallet, MainMenu (START deletes the save), DebugMenu and DebugMenuGameBindings (clickable panel, new actions). Originals are backed up in `.claude-backup/2026-11-02/` at the project root (the AJ-75 starting point is in `.claude-backup/2026-11-02-pre-save/`).
