# Fire Worm and checkpoint skeleton shops

MainMenu START loads ProceduralPillar. The required Fire Worm platform is three rows before the first blue checkpoint. Defeating the boss opens the upper route. Animated Memento Mori skeleton merchants are installed at every checkpoint (200, 400 and 800). AnthonyScene retains a merchant and a separate boss arena for tweaking.

## Boss settings

FireWormDefinition.asset controls HP, contact damage and movement. BossAttacks.asset controls fireball/volley/ground-wave damage, slam damage, attack ranges, windup/active frames, recovery and phase thresholds. These balance settings were preserved during the media upgrade.

FireWormPresentation.asset contains every source animation: Idle 9, Walk 9, Attack 16, Hit 3, Death 8, Fireball 6 and Explosion 7 frames. Fireball Attack and Slam Attack are now separate editable sequences. Fireball uses the full attack strip with breathing frames timed to projectile release. The pack supplies no dedicated slam strip; Slam Attack uses nine non-breathing source poses plus editable lift, lean and squash, followed by the explosion strip as a ground impact. Body colliders remain stable. Full hit and death sequences play from their first frame.

Slamming uses the existing BossAttacks windup and active-frame timing. Ordinary frame loops use Frames Per Second. The legacy Attack array remains a fallback for older presentation sets.

## Battle music

Battle Music, Music Volume and Music Fade Seconds are on FireWormPresentation.asset. The supplied Japanese boss music is imported as a streaming loop. It begins when the player lands in the boss arena, fades in, ducks existing 2D looping background audio, and stops on boss/player death or scene unload. Previous background volumes are restored. Actor spawn/streaming before the player arrives does not start the music.

## Skeleton merchant

Assets/Prefabs/NPCs/MementoMoriShopkeeper.prefab is used on production checkpoints. Its ShopkeeperAnimation component has 16 frames at 12.5 FPS, matching the source ASE's 80 ms timing. The PNG is sliced row by row from the top left, using 128 x 96 cells. The original PNG and editable ASE source are retained under Assets/Art/Shops. The friendly merchant has no solid collider, enemy health or contact damage.

VerticalPillar.asset > Checkpoint Shop Prefab and Checkpoint Shop Offset control placement. The merchant is parented to its streamed chunk without inheriting platform scale. Repeated refreshes do not duplicate it.

E / Xbox Y opens the shop; Enter/Space / Xbox A confirms; Escape / Xbox B closes; arrows / stick / D-pad navigate. Shopping pauses gameplay and locks combat inputs until held action buttons are released.

ShopCatalog.asset controls offers and their prices, amounts and limits. Open Sound, Open Sound Volume and Open Sound Pitch control the creepy whisper, which plays once on opening and stops on closing. Purchase Sound remains independent.

PrototypeWallet still starts with 500 clearly labeled test coins. There is no currency earning system yet. Vitality costs 100 (+1 max/current health), Sharpen 150 (+1 melee/ranged damage), Air Step 250 (+1 air jump, three jumps total). Purchases persist between streamed checkpoints; coins and upgrades reset on a scene reload. CurrencyWallet is the interface for a future real economy.

## Authoring tools

Tools > Anthony > Import Boss and Skeleton Media refreshes source sprite/audio assignments and the merchant prefab while preserving boss balance values. It also upgrades the existing AnthonyScene merchant in place. Tools > Anthony > Build or Reset AnthonyScene replaces the test layout; it now uses the animated merchant prefab. Configure Animated Boss and Shop Assets intentionally resets initial boss balance defaults before importing the current media.

## Source records

Fire Worm: https://luizmelo.itch.io/fire-worm — CC0; source inventory is Assets/Art/Fire Worm/AssetInventory.md.
Whisper: Whisper.wav by PlumForestPodcast — https://freesound.org/people/PlumForestPodcast/sounds/517868/ — CC0. Source and license links are in Assets/Audio/Shops/Sources.txt.
Skeleton and battle music were supplied by the user; source files/provenance are retained in their asset folders.

## Verification

44 integrated Unity Play Mode checks passed after merging the latest teammate dash/death/parry animation changes. Coverage includes all source frames, separate slam/cast visuals, stable body colliders, full hit/death playback, boss music entry/loop/fade/duck/restore, both victory and player-death exits, checkpoint spawning/deduplication, E/Xbox Y interaction, whisper playback while paused, upgrade purchasing and persistence between streamed checkpoints. Skeleton proportions and slam impact were also reviewed in Game view.
