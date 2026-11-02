using UnityEngine;

[CreateAssetMenu(menuName = "Ascend Jigoku/Required Boss Encounter")]
public sealed class BossEncounterSettings : ScriptableObject
{
    public GameObject bossPrefab;
    [Min(0)] public int checkpointMarkerIndex;
    [Min(3)] public int rowsBeforeCheckpoint = 3;
    [Min(18)] public float combatWidth = 32;
    [Min(6)] public float ceilingHeight = 8.5f;
    public Sprite barrierSprite;
    public Color barrierColor = new Color(1, 0.25f, 0.08f, 0.55f);
}
