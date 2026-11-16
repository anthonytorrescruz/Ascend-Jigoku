using UnityEngine;

public enum WeaponId { Katana, Kanabo }

// Static tuning for the two demo weapons. Frame counts add to the CombatSettings player values.
public sealed class Weapon
{
    public WeaponId Id;
    public string Name, AbilityName, AbilityShort;
    public Vector2 SizeScale = Vector2.one;
    public int BonusDamage, ExtraStartupFrames, ExtraActiveFrames, ExtraRecoveryFrames;
    public float Knockback = 1, AbilityCooldown;

    public static readonly Weapon Katana = new Weapon
    {
        Id = WeaponId.Katana, Name = "KATANA", AbilityName = "Flash Cut", AbilityShort = "CUT", AbilityCooldown = 5f
    };

    // Slow, wide and heavy: one hit trades speed for damage and knockback.
    public static readonly Weapon Kanabo = new Weapon
    {
        Id = WeaponId.Kanabo, Name = "ONI KANABO", AbilityName = "Quake Slam", AbilityShort = "QUAKE", AbilityCooldown = 10f,
        SizeScale = new Vector2(1.5f, 1.3f), BonusDamage = 1, ExtraStartupFrames = 8, ExtraActiveFrames = 3, ExtraRecoveryFrames = 16, Knockback = 2.2f
    };

    public static Weapon Get(WeaponId id) => id == WeaponId.Kanabo ? Kanabo : Katana;
}
