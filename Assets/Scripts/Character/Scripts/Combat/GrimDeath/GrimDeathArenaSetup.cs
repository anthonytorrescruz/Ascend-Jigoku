using UnityEngine;

// A direct-entry practice arena; production uses the streamed level's second boss gate.
public sealed class GrimDeathArenaSetup : MonoBehaviour
{
    public GrimDeathSettings settings;
    public Collider2D floor;
    public PlayerController player;
    public GrimDeathEncounter Encounter { get; private set; }
    private void Start()
    {
        var room = new GameObject("Grim Death practice encounter");
        room.transform.position = new Vector3(floor.bounds.center.x, floor.bounds.max.y, 0);
        Encounter = room.AddComponent<GrimDeathEncounter>();
        Encounter.Initialize(settings, floor, player, () => { });
    }
}
