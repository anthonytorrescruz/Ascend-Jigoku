# Chunked pillar

Main menu Start begins a new run. Retry keeps that run's seed and activated
checkpoint. Runs are held in memory for this play session, not saved to disk.

F1 shows a world panel in the top-right corner: sampled FPS/frame time, seed,
height, live chunk/ledge/enemy counts, queued ledges, effective loading distances,
construction budget and checkpoint. Its local map follows the climb: green
ledges exist, orange ledges are queued, gray ledges are unloaded seed previews.
Cyan lines mark height events; the yellow dot is the player and the white box
is the camera. Previews only calculate positions and never load terrain.

Tune **Assets/Settings/VerticalPillar.asset**:

- Pillar Height: total climb height (1200 by default).
- Chunk Height / Platforms Per Chunk: 40 units / 10 ledges. Height is reduced
  if needed to fit the player's available jumps; F1 shows loaded chunks/ledges.
- Load Above / Keep Below: 64 / 28 units. Unload Buffer adds a 12-unit margin
  to prevent chunks flickering in and out at a boundary. Camera/fall safety
  minimums are enforced when a requested distance would be too small.
- Platforms Per Frame: new ledge construction budget (3). The arrival chunk
  loads immediately on start/respawn; the rest build ahead over later frames.
- Platform Width / Thickness: chunky ledges, 6.5–9 wide and 1.5 thick.
- Zigzag Width / Variation: side-to-side route shape. Jump Safety caps gaps
  using the player's jump count, speed and gravity. Default rises require the
  existing double jump. Dash and mandatory enemy pogo routes are not generated.
- Height Markers: checkpoint/shop/boss hooks. Heights snap to the nearest safe
  landing. Checkpoint markers are cyan and update the death respawn position.
  Optional prefabs attach to these ledges. The generator's On Height Reached
  event sends the marker ID once when the player lands there; connect content
  behavior in the Inspector. New runs reset these events.
- Fall Distance / Damage / Return Seconds: a drop of 12 units since the last
  landing or airborne peak costs one health and sends the player back upward
  to the latest safe ledge. Controls/collisions pause during the short return,
  then restore with brief protection. A fatal fall opens the normal death flow.

Only nearby chunks and enemies exist as objects. Old terrain, colliders and
AI unload. Revisiting reproduces the same terrain, keeps surviving enemy health
and leaves defeated enemies defeated. Recreated survivors return to their
original spawn positions. Death Retry resets enemies and starts at the current
checkpoint. New menu Start creates a new seed and clears checkpoints.

Keep the generator unscaled/unrotated. ClimbPlatform uses a centered unit box
on the Ground layer and a one-way effector; replacing the art should preserve
that structure. Leave space around optional marker prefabs for the arrival.
The orange summit remains a placeholder for later exit/boss content.

CameraFollow still has its tiny dead zone, center safe region and 1.5-unit hard
limit. The procedural scene uses a slightly wider view to show the larger gaps.
