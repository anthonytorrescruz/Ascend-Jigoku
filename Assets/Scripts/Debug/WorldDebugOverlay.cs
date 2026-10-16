// F1 world diagnostics. The map reads chunk state; previews never spawn objects.
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AscendJigoku.Debugging
{
    public sealed class WorldDebugOverlay : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private VerticalLevelGenerator world;
        private float sampleTime, fps, nextScan;
        private int frames;
        private GUIStyle label, title, small;
        private static readonly Color Loaded = new Color(0.4f, 0.85f, 0.55f);
        private static readonly Color Queued = new Color(1f, 0.65f, 0.2f);
        private static readonly Color Preview = new Color(0.35f, 0.38f, 0.43f);
        private static readonly Color Marker = new Color(0.3f, 0.85f, 1f);

        private void Update()
        {
            sampleTime += Time.unscaledDeltaTime; frames++;
            if (sampleTime >= 0.5f)
            { fps = frames / sampleTime; frames = 0; sampleTime = 0; }
            if (!DebugMenu.IsOpen || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;
            if (world == null || !world.isActiveAndEnabled)
                world = FindAnyObjectByType<VerticalLevelGenerator>();
        }

        private void OnGUI()
        {
            if (!DebugMenu.IsOpen) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
                title = new GUIStyle(label) { fontSize = 16, fontStyle = FontStyle.Bold };
                small = new GUIStyle(label) { fontSize = 11 };
            }
            Matrix4x4 previous = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.depth = -5;
            float scale = DebugMenu.DisplayScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            bool ready = world != null && world.isActiveAndEnabled && world.Player != null
                && world.Settings != null && world.Metrics.RowHeight > 0;
            float width = Mathf.Min(304, Screen.width / scale - 24);
            var panel = new Rect(Screen.width / scale - width - 12, 12, width, ready ? 462 : 96);
            DebugMenu.DrawBackdrop(panel);
            float x = panel.x + 12, y = panel.y + 8, contentWidth = width - 24;
            GUI.Label(new Rect(x, y, contentWidth, 24), "WORLD DEBUG                     F1", title); y += 26;
            Line(x, ref y, contentWidth, $"{fps:F0} FPS  /  {(fps > 0 ? 1000 / fps : 0):F1} ms   |   Time x{Time.timeScale:F1}");
            Line(x, ref y, contentWidth, SceneManager.GetActiveScene().name);
            if (ready)
            {
                var metrics = world.Metrics;
                var settings = world.Settings;
                Vector3 position = world.Player.transform.position - world.transform.position;
                int chunk = Mathf.Clamp(Mathf.FloorToInt(position.y / metrics.ChunkHeight), 0, metrics.LastRow / metrics.RowsPerChunk);
                Line(x, ref y, contentWidth, $"Seed: {world.CurrentSeed}");
                Line(x, ref y, contentWidth, $"Height: {position.y:F1} / {metrics.LastRow * metrics.RowHeight:F0}   |   Chunk: {chunk}");
                Line(x, ref y, contentWidth, $"Loaded: {world.LoadedChunkCount} chunks / {world.ActivePlatformCount} ledges");
                Line(x, ref y, contentWidth, $"Queue: {world.PendingPlatformCount} ledges   |   Enemies: {world.ActiveEnemyCount}");
                Line(x, ref y, contentWidth, $"Chunk: {metrics.ChunkHeight:F0}u / {metrics.RowsPerChunk} ledges   |   Budget: {Mathf.Clamp(settings.platformsPerFrame, 1, 12)}/frame");
                Line(x, ref y, contentWidth, $"Width: {settings.pillarWidth:F0}u   |   Solid rock / 4 shapes");
                Line(x, ref y, contentWidth, $"Load: +{world.LoadAboveDistance:F0} / -{world.KeepBelowDistance:F0}u   |   Buffer: {settings.unloadBuffer:F0}u");
                Line(x, ref y, contentWidth, $"Checkpoint: {world.GetLanding(world.CheckpointRow).Center.y:F0}u   |   {(world.IsFallBouncing ? "Fall bounce" : "Streaming")}");
                y += 4;
                DrawMap(new Rect(x, y, contentWidth, 160), position);
                y += 164;
                GUI.Label(new Rect(x, y, contentWidth, 16), "Green loaded   Orange queued   Gray unloaded", small); y += 16;
                GUI.Label(new Rect(x, y, contentWidth, 16), "Yellow player   Cyan marker   White camera", small);
            }
            else GUI.Label(new Rect(x, y, contentWidth, 18), "No active pillar generator", small);
            GUI.matrix = previous;
            GUI.depth = previousDepth;
        }

        private void Line(float x, ref float y, float width, string text)
        { GUI.Label(new Rect(x, y, width, 20), text, label); y += 20; }

        private void DrawMap(Rect rect, Vector3 playerPosition)
        {
            var metrics = world.Metrics;
            var settings = world.Settings;
            float span = Mathf.Max(80, world.LoadAboveDistance + world.KeepBelowDistance + metrics.ChunkHeight * 2);
            float low = Mathf.Max(0, playerPosition.y - world.KeepBelowDistance - metrics.ChunkHeight);
            float high = low + span;
            float towerWidth = Mathf.Max(8, settings.pillarWidth);
            Fill(rect, new Color(0.025f, 0.03f, 0.04f));
            GUI.BeginGroup(rect);
            float MapY(float height) => (high - height) / span * rect.height;
            float MapX(float value) => (value / towerWidth + 0.5f) * rect.width;
            int firstChunk = Mathf.Max(0, Mathf.FloorToInt(low / metrics.ChunkHeight));
            int lastChunk = Mathf.Min(metrics.LastRow / metrics.RowsPerChunk, Mathf.FloorToInt(high / metrics.ChunkHeight));
            for (int index = firstChunk; index <= lastChunk; index++)
            {
                bool loaded = world.TryGetChunkProgress(index, out int built, out int total);
                float top = MapY((index + 1) * metrics.ChunkHeight);
                float bottom = MapY(index * metrics.ChunkHeight);
                if (loaded) Fill(new Rect(0, top, rect.width, bottom - top), new Color(0.15f, 0.3f, 0.24f, 0.35f));
                Fill(new Rect(0, bottom, rect.width, 1), Preview);
                GUI.Label(new Rect(3, Mathf.Clamp(top, 0, rect.height - 15), 120, 16),
                    $"C{index}  {(loaded ? built + "/" + total : "unloaded")}", small);
            }
            // Bound preview work even when settings describe a very large pillar.
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(low / metrics.RowHeight) - 1);
            int lastRow = Mathf.Min(metrics.LastRow, Mathf.CeilToInt(high / metrics.RowHeight) + 1);
            int stride = Mathf.Max(1, Mathf.CeilToInt((lastRow - firstRow + 1) / 160f));
            for (int row = firstRow; row <= lastRow; row += stride)
            {
                var landing = world.GetLanding(row);
                bool loaded = world.TryGetChunkProgress(row / metrics.RowsPerChunk, out int built, out _);
                bool exists = loaded && row % metrics.RowsPerChunk < built;
                Color color = exists ? Loaded : loaded ? Queued : Preview;
                float left = MapX(landing.Center.x - landing.Width * 0.5f);
                Fill(new Rect(left, MapY(landing.Center.y), landing.Width / towerWidth * rect.width,
                    Mathf.Max(2, landing.Thickness / span * rect.height)), color);
            }
            if (settings.heightMarkers != null)
                foreach (var marker in settings.heightMarkers)
                {
                    if (marker == null) continue;
                    float height = world.GetLanding(VerticalLayout.MarkerRow(marker, metrics)).Center.y;
                    if (height < low || height > high) continue;
                    Fill(new Rect(0, MapY(height), rect.width, 1), Marker);
                }
            Camera camera = Camera.main;
            if (camera != null && camera.orthographic)
            {
                Vector3 center = camera.transform.position - world.transform.position;
                float halfHeight = camera.orthographicSize, halfWidth = halfHeight * camera.aspect;
                var view = new Rect(MapX(center.x - halfWidth), MapY(center.y + halfHeight),
                    halfWidth * 2 / towerWidth * rect.width, halfHeight * 2 / span * rect.height);
                Fill(new Rect(view.x, view.y, view.width, 1), Color.white);
                Fill(new Rect(view.x, view.yMax, view.width, 1), Color.white);
                Fill(new Rect(view.x, view.y, 1, view.height), Color.white);
                Fill(new Rect(view.xMax, view.y, 1, view.height), Color.white);
            }
            Fill(new Rect(MapX(playerPosition.x) - 3, MapY(playerPosition.y) - 3, 6, 6), Color.yellow);
            GUI.EndGroup();
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
#endif
    }
}
