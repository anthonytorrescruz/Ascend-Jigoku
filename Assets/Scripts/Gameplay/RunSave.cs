using System;
using System.IO;
using UnityEngine;

namespace AscendJigoku.Gameplay
{
    // Saved run (AJ-75). One slot, JSON in Application.persistentDataPath.
    // Written automatically by VerticalLevelGenerator when a run starts, when a checkpoint is activated,
    // after a shop purchase and when the boss is defeated. Progress after the last save (coins, upgrades)
    // is not kept: death and Continue both restore this snapshot.
    [Serializable]
    public sealed class RunSaveData
    {
        public int version;
        public string scene;
        public string settings;
        public int seed;
        public int checkpointRow;
        public float checkpointHeight;
        public bool bossDefeated;
        public bool finalBossDefeated;
        // Player state captured at the last checkpoint/purchase. hasSnapshot is false before the first checkpoint.
        public bool hasSnapshot;
        public int[] purchases = new int[3];
        public int[] upgradeAmounts = new int[3];
        public int coins = -1;
        public string savedAtUtc;
    }

    public static class RunSave
    {
        public const int Version = 1;
        public static string FilePath => Path.Combine(Application.persistentDataPath, "run-save.json");

        public static bool HasSave => TryLoad(out _);

        public static string BackupPath => FilePath + ".bak";

        // Loads the main file, falling back to the .bak copy if it is missing or unreadable.
        public static bool TryLoad(out RunSaveData data)
        {
            if (TryRead(FilePath, out data)) return true;
            return TryRead(BackupPath, out data);
        }

        private static bool TryRead(string path, out RunSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                var candidate = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(path));
                if (candidate == null || candidate.version != Version || string.IsNullOrEmpty(candidate.scene)) return false;
                candidate.purchases = Fit(candidate.purchases);
                candidate.upgradeAmounts = Fit(candidate.upgradeAmounts);
                data = candidate;
                return true;
            }
            catch (Exception) { return false; } // A corrupt primary may still have a valid backup.
        }

        // Writes to a temp file then swaps it in, keeping the previous good save as .bak, so a crash mid-write can't lose the run.
        public static void Write(RunSaveData data)
        {
            if (data == null) return;
            data.version = Version;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            try
            {
                string temp = FilePath + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Replace on the same volume atomically. Never delete the live slot first.
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, TryRead(FilePath, out _) ? BackupPath : null);
                else File.Move(temp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("Run could not be saved; previous save retained: " + e.Message); }
        }

        public static void Delete()
        {
            // Intentional New Run/completion removes recovery files too, so Continue cannot resurrect an old run.
            foreach (string path in new[] { FilePath, BackupPath, FilePath + ".tmp" })
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception e) { Debug.LogWarning("Saved run could not be deleted: " + e.Message); }
        }

        // Owned weapons ride in the high bits of the last purchase counter so the run snapshot carries them
        // with no extra field: Continue and death-reload both restore what was owned at the last save.
        private const int WeaponShift = 8;
        public static int PackWeapons(int count, int weaponMask) => (count & 0xFF) | (weaponMask << WeaponShift);
        public static int UnpackCount(int packed) => packed & 0xFF;
        public static int UnpackWeapons(int packed) => packed >> WeaponShift;

        // Pads or trims a saved array to exactly 3 entries (one per UpgradeKind).
        public static int[] Fit(int[] values)
        {
            var result = new int[3];
            if (values != null) Array.Copy(values, result, Mathf.Min(values.Length, result.Length));
            return result;
        }
    }
}
