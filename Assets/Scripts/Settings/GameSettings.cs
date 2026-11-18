using System;
using UnityEngine;

namespace AscendJigoku.Settings
{
    // Player-facing options. Values persist in PlayerPrefs and apply on startup.
    // Read the properties from gameplay code; the settings screen writes them and calls Save().
    public static class GameSettings
    {
        private const string Prefix = "settings.v1.";

        // Raised after any setter or reset; HUDs and effects subscribe to refresh themselves.
        public static event Action Changed;

        // Audio. Master drives AudioListener; Music/SFX are multiplied in at each play site.
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;

        // Accessibility.
        public static float ScreenShake { get; private set; } = 1f;
        public static bool HitStop { get; private set; } = true;
        public static bool ReduceFlashing { get; private set; }
        public static bool StrongTelegraphs { get; private set; }
        public static bool DamageVignette { get; private set; } = true;
        public static float GameSpeed { get; private set; } = 1f;

        // Gameplay / display.
        public static bool InstantRespawn { get; private set; }
        public static bool ShowTimer { get; private set; }
        public static bool Fullscreen { get; private set; } = true;
        public static int WindowSize { get; private set; } = 1;
        public static bool VSync { get; private set; } = true;
        public static int FrameCap { get; private set; }

        public const float MinGameSpeed = 0.5f;
        public static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(960, 540), new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080)
        };
        // 0 means uncapped.
        public static readonly int[] FrameCaps = { 0, 60, 120, 144, 240 };

        // Time scale used whenever a menu hands control back to gameplay.
        public static float GameplayTimeScale => GameSpeed;

        // Runs before the first scene so saved volume, window size and frame cap are in effect from the start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            Load();
            Apply();
            ApplyDisplay();
        }

        // Reads every saved value, clamping it so a hand-edited or old save can't produce an invalid setting.
        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "master", 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "music", 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "sfx", 1f));
            ScreenShake = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "shake", 1f));
            HitStop = PlayerPrefs.GetInt(Prefix + "hitstop", 1) != 0;
            ReduceFlashing = PlayerPrefs.GetInt(Prefix + "reduceFlashing", 0) != 0;
            StrongTelegraphs = PlayerPrefs.GetInt(Prefix + "strongTelegraphs", 0) != 0;
            DamageVignette = PlayerPrefs.GetInt(Prefix + "vignette", 1) != 0;
            GameSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "gameSpeed", 1f), MinGameSpeed, 1f);
            InstantRespawn = PlayerPrefs.GetInt(Prefix + "instantRespawn", 0) != 0;
            ShowTimer = PlayerPrefs.GetInt(Prefix + "timer", 0) != 0;
            Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            WindowSize = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "windowSize", 1), 0, WindowSizes.Length - 1);
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
            FrameCap = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "frameCap", 0));
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "shake", ScreenShake);
            PlayerPrefs.SetInt(Prefix + "hitstop", HitStop ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "reduceFlashing", ReduceFlashing ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "strongTelegraphs", StrongTelegraphs ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vignette", DamageVignette ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "gameSpeed", GameSpeed);
            PlayerPrefs.SetInt(Prefix + "instantRespawn", InstantRespawn ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "timer", ShowTimer ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "windowSize", WindowSize);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "frameCap", FrameCap);
            PlayerPrefs.Save();
        }

        // Setters change the value and apply it immediately; nothing is written to disk until Save() is called.
        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            Apply();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            Apply();
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            Apply();
        }

        public static void SetScreenShake(float value)
        {
            ScreenShake = Mathf.Clamp01(value);
            Apply();
        }

        public static void SetHitStop(bool value)
        {
            HitStop = value;
            Apply();
        }

        public static void SetReduceFlashing(bool value)
        {
            ReduceFlashing = value;
            Apply();
        }

        public static void SetStrongTelegraphs(bool value)
        {
            StrongTelegraphs = value;
            Apply();
        }

        public static void SetDamageVignette(bool value)
        {
            DamageVignette = value;
            Apply();
        }

        // Rounded to 0.1. Takes effect when gameplay next resumes (menus own the time scale while open).
        public static void SetGameSpeed(float value)
        {
            GameSpeed = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, MinGameSpeed, 1f);
            Apply();
        }

        public static void SetInstantRespawn(bool value)
        {
            InstantRespawn = value;
            Apply();
        }

        public static void SetShowTimer(bool value)
        {
            ShowTimer = value;
            Apply();
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            ApplyDisplay();
        }

        public static void SetWindowSize(int index)
        {
            WindowSize = Mathf.Clamp(index, 0, WindowSizes.Length - 1);
            ApplyDisplay();
        }

        public static void SetVSync(bool value)
        {
            VSync = value;
            ApplyDisplay();
        }

        public static void SetFrameCap(int fps)
        {
            FrameCap = Mathf.Max(0, fps);
            ApplyDisplay();
        }

        public static void ResetDisplayToDefaults()
        {
            Fullscreen = true;
            WindowSize = 1;
            VSync = true;
            FrameCap = 0;
            ApplyDisplay();
        }

        // Audio, accessibility and gameplay options only; display options have their own reset above.
        public static void ResetToDefaults()
        {
            InstantRespawn = ShowTimer = false;
            MasterVolume = MusicVolume = SfxVolume = 1f;
            ScreenShake = 1f;
            HitStop = true;
            ReduceFlashing = false;
            StrongTelegraphs = false;
            DamageVignette = true;
            GameSpeed = 1f;
            Apply();
        }

        // Window size only matters when windowed; the editor's Game view is left alone.
        private static void ApplyDisplay()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FrameCap > 0 ? FrameCap : -1;
            if (Application.isEditor) return;
            var size = WindowSizes[WindowSize];
            if (Fullscreen)
            {
                var native = Screen.currentResolution;
                Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            }
            else Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        private static void Apply()
        {
            AudioListener.volume = MasterVolume;
            Changed?.Invoke();
        }
    }
}
