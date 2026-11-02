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

        public static bool TryLoad(out RunSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(FilePath)) return false;
                data = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
                if (data == null || data.version != Version || string.IsNullOrEmpty(data.scene)) { data = null; return false; }
                data.purchases = Fit(data.purchases);
                data.upgradeAmounts = Fit(data.upgradeAmounts);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Saved run could not be read and was ignored: " + e.Message);
                data = null;
                return false;
            }
        }

        public static void Write(RunSaveData data)
        {
            if (data == null) return;
            data.version = Version;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            try
            {
                // Write beside the save first so a crash mid-write cannot corrupt the existing slot.
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e) { Debug.LogWarning("Run could not be saved: " + e.Message); }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning("Saved run could not be deleted: " + e.Message); }
        }

        public static int[] Fit(int[] values)
        {
            var result = new int[3];
            if (values != null) Array.Copy(values, result, Mathf.Min(values.Length, result.Length));
            return result;
        }
    }
}
