using AscendJigoku.Gameplay;
using UnityEditor;
using UnityEngine;

// Editor helpers for the saved-run slot (AJ-75). The editor and builds use separate persistentDataPath folders on most platforms.
public static class RunSaveTools
{
    [MenuItem("Tools/Ascend Jigoku/Saved Run/Delete Saved Run")]
    private static void DeleteSave()
    {
        RunSave.Delete();
        Debug.Log("Saved run deleted: " + RunSave.FilePath);
    }

    [MenuItem("Tools/Ascend Jigoku/Saved Run/Show Save File")]
    private static void Reveal()
    {
        if (System.IO.File.Exists(RunSave.FilePath)) EditorUtility.RevealInFinder(RunSave.FilePath);
        else Debug.Log("No saved run yet. It will be written to: " + RunSave.FilePath);
    }

    [MenuItem("Tools/Ascend Jigoku/Saved Run/Log Save Contents")]
    private static void LogContents()
    {
        if (RunSave.TryLoad(out var save)) Debug.Log(JsonUtility.ToJson(save, true));
        else Debug.Log("No readable saved run at " + RunSave.FilePath);
    }
}
