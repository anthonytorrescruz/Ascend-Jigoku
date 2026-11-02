using System;
using UnityEngine;

namespace AscendJigoku.Settings
{
    // Player-facing options. Values persist in PlayerPrefs and apply on startup.
    // Read the properties from gameplay code; the settings screen writes them and calls Save().
    public static class GameSettings
    {
        private const string Prefix = "settings.v1.";

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

        public const float MinGameSpeed = 0.5f;

        // Time scale used whenever a menu hands control back to gameplay.
        public static float GameplayTimeScale => GameSpeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            Load();
            Apply();
        }

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
            PlayerPrefs.Save();
        }

        public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); Apply(); }
        public static void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); Apply(); }
        public static void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); Apply(); }
        public static void SetScreenShake(float value) { ScreenShake = Mathf.Clamp01(value); Apply(); }
        public static void SetHitStop(bool value) { HitStop = value; Apply(); }
        public static void SetReduceFlashing(bool value) { ReduceFlashing = value; Apply(); }
        public static void SetStrongTelegraphs(bool value) { StrongTelegraphs = value; Apply(); }
        public static void SetDamageVignette(bool value) { DamageVignette = value; Apply(); }
        // Takes effect when gameplay next resumes (menus own the time scale while open).
        public static void SetGameSpeed(float value) { GameSpeed = Mathf.Clamp(Mathf.Round(value * 10f) / 10f, MinGameSpeed, 1f); Apply(); }

        public static void ResetToDefaults()
        {
            MasterVolume = MusicVolume = SfxVolume = 1f;
            ScreenShake = 1f; HitStop = true; ReduceFlashing = false; StrongTelegraphs = false; DamageVignette = true; GameSpeed = 1f;
            Apply();
        }

        private static void Apply()
        {
            AudioListener.volume = MasterVolume;
            Changed?.Invoke();
        }
    }
}
