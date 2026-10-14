using UnityEngine;

// Identifies a weapon collider so striking it does not damage its owner's body.
public class CombatAttackHitbox : MonoBehaviour
{
    public CombatAttack Owner { get; set; }
}
