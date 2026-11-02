# Boss and checkpoint shop playtest

Open Assets/Scenes/AnthonyScene.unity and enter Play Mode. The blue starting platform has the merchant. Cross the gap to the right to fight the animated Fire Worm. Old guide/healer dialogue and ember quests have been removed.

## Production encounter

MainMenu START loads ProceduralPillar. VerticalPillar.asset now references BossEncounter.asset. The generator replaces the landing three rows before the first blue checkpoint with a broad, grey, one-way boss floor. Enter the center to start the fight. Visible seals and withheld upper platforms block the checkpoint until the boss dies. Victory persists through same-run deaths/reloads; MainMenu START starts a fresh run. Only the boss is deployed; the shop remains in AnthonyScene.

## Boss tuning

- FireWormDefinition.asset: 45 HP, movement, contact damage, stopping distance and defenses.
- BossAttacks.asset: three phases at 30 and 15 HP; aimed fireballs, three/five-shot fan volleys, slam, two horizontal ground waves from phase two. Windup, active frames, recovery, projectile size/speed, spread and cooldowns remain editable.
- FireWormPresentation.asset: idle/walk/attack/hit/death and fireball/explosion frames, five sound clips, volume and animation rate.
- BossEncounter.asset: checkpoint marker index, rows before checkpoint, room width/height and barrier color.
- FireWormBossAnimated.prefab: solid head/tail share health and contact damage. Source prototype prefab remains available for EnemyPlayground. A player obstructing a safe head turn can still be targeted with a ranged attack; solid colliders never turn through the player. Dedicated parry and repeatable pogo still use the existing combat resolver.

The Fire Worm pack is CC0. Boss/shop sounds are Kenney CC0; original pack licenses and exact source mapping are in Assets/Audio/Boss.

## Shop skeleton

Keyboard E / Xbox Y talks to the merchant. Enter/Space or Xbox A confirms; Esc / Xbox B closes; arrows/stick/D-pad navigate. Gameplay pauses and combat inputs are locked while shopping; releasing action buttons is required before combat resumes.

ShopCatalog.asset controls prices, price growth, upgrade amounts and purchase limits. Initial offers: Vitality 100 (+1 max health, restores one), Sharpen 150 (+1 melee and ranged damage), Air Step 250 (+1 air jump, three jumps total). Health/damage prices rise by 1.5 after purchase. Air Step is limited to one purchase. Failed/limited purchases never charge coins.

PrototypeWallet on AnthonyScene Player starts with 500 explicitly labeled test coins. It implements CurrencyWallet, so a future real wallet can be assigned without changing purchases/UI. No earning system or persistence is implemented: coins and upgrades reset when the scene reloads. Shop content is not installed in the production level.

## Authoring

Tools > Anthony > Configure Animated Boss and Shop Assets prepares the shared assets. It resets the initial boss defaults and uniformly slices the source strips; use intentionally.
Tools > Anthony > Build or Reset AnthonyScene recreates the test scene. Save scene edits before using it; it replaces the test layout.
Tools > Anthony > Build Windows Playtest packages AnthonyScene only.

53 checks passed in Unity Play Mode, including simulated keyboard/Xbox controls and actual three-jump input behavior. Verification covered shop charging/caps/resume, sprite/audio assignments, 45 HP phases, fireball/volley/shockwave attacks, death delay, MainMenu START, arena entry/seals, boss-gated checkpoint, same-run persistence and new-run reset. Physical controller and teammate-machine feel tuning remain useful.
