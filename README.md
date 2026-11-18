# Ascend Jigoku

A single-player 2D action platformer built in Unity for CSCI 3300 Software Engineering at the University of North Georgia. Ascend a procedural pillar, fight enemies with directional melee attacks and spells, buy upgrades at checkpoints, and defeat Fire Worm and Grim Death.

**Demo goal:** defeat both bosses and reach the second checkpoint to trigger the ending.

## Play or open the project

A final prebuilt release has not been published yet. Check [GitHub Releases](https://github.com/anthonytorrescruz/Ascend-Jigoku/releases) for downloadable builds. GitHub's automatic source ZIP is the Unity project, not a runnable game.

To run from source:

1. Install **Unity Hub** and **Unity Editor 6000.5.9f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`. Install Windows Build Support when creating a Windows player.
2. Clone or download this repository. The selected project folder must contain `Assets`, `Packages`, and `ProjectSettings`.
3. In Unity Hub, select **Add project from disk** and choose that folder.
4. Let Unity restore the packages in `Packages/manifest.json` and finish importing. Do not enter Play Mode while compilation is incomplete.
5. Open **Assets/Scenes/MainMenu.unity**, then press Play and choose START or TUTORIAL. START launches ProceduralPillar. The main menu adds Settings, Tutorial, and Continue at runtime.

No database, server, API key, or Unity Version Control credentials are needed to play the local game. An internet connection may be needed for the initial Unity/package installation.

For a Windows release, extract the **entire** release ZIP and run its `.exe`; keep its `_Data` folder, `UnityPlayer.dll`, and other included files together.

## Default controls

| Action | Keyboard | Xbox controller |
| --- | --- | --- |
| Move / aim direction | WASD or arrow keys | Left stick or D-pad |
| Jump | Space | A |
| Attack | J | X |
| Upward attack | Hold W / Up and attack | Hold up and attack |
| Downward pogo attack | While airborne, hold S / Down and attack | While airborne, hold down and attack |
| Dash | K | RB |
| Dedicated parry | L | LB |
| Ranged spell | Tap and release ; | Tap and release B |
| Heal one health | Hold ; for about 1.5 seconds | Hold B for about 1.5 seconds |
| Talk / shop | E | Y |
| Weapon ability | U | RT |
| Swap owned weapon | I | LT |
| Pause | Escape | Start / Menu |

Keyboard bindings can be changed in Settings; gamepad bindings are fixed. In-game prompts reflect the active input device. A downward pogo requires an airborne hit on a pogoable body/attack and does not consume a normal jump.

## Main features

- Procedural platforms and enemies streamed around the player rather than generating the whole level at once.
- Directional melee, horizontal damage knockback, repeatable pogo, ranged spells, healing, weapon unlocks, and upgrades.
- Dedicated timed parry. A success grants a brief protected counterattack window; an unsuccessful attempt leaves recovery vulnerability.
- Fire Worm followed by Grim Death, with telegraphs, boss health bars, music, and distinct attack patterns. Grim Death currently has 10 HP.
- Checkpoints heal and clear nearby threats; the ending stops combat and physics underneath its cinematic.
- Memento Mori shopkeeper offering health, damage, and jump upgrades.
- Pause/settings menus, keyboard rebinding, audio/display/accessibility settings, death/retry, checkpoint Continue, tutorial, and ending.

The final integrated build still requires the team's acceptance playthrough. Historical component checks are not a substitute for release validation; see [testing](docs/TESTING.md).

## Project guide

- [Architecture and tuning](docs/ARCHITECTURE.md)
- [Team and contributions](docs/TEAM.md)
- [Rubric review and submission checklist](docs/SUBMISSION_READINESS.md)
- [Acceptance test checklist and historical results](docs/TESTING.md)
- [Complete demo recording plan](docs/DEMO.md)
- [Windows release and browser-play instructions](docs/RELEASING.md)
- [Third-party credits and permission review](THIRD_PARTY.md)

`Assets/Scenes/EnemyPlayground.unity` and `Assets/Scenes/GrimDeathPlayground.unity` are development areas. Use MainMenu for the actual game and submission build. Development/debug tools are not required for normal completion.

## Team

All four team members contributed programming:

| Member | Main focus confirmed by the team |
| --- | --- |
| Anthony Torres-Cruz | Combat programming |
| Andrew Miller | Settings and debug-menu programming |
| Jake Peck | Visual and audio implementation |
| Joey Terrell | Visual and audio implementation |

Third-party art/audio remains owned by its respective creators. Repository access does not grant permission to redistribute those assets; see the permission review before publishing new source/build packages.
