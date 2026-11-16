using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(EnemyBrain))]
[DisallowMultipleComponent]
public sealed class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private bool artFacesRight = true;
    [Min(0)] [SerializeField] private float moveThreshold = 0.1f;

    private static readonly int Moving = Animator.StringToHash("Moving");
    private static readonly int WindingUp = Animator.StringToHash("WindingUp");
    private static readonly int Attacking = Animator.StringToHash("Attacking");
    private static readonly int Hurt = Animator.StringToHash("Hurt");
    private static readonly int Dead = Animator.StringToHash("Dead");
    private static readonly int Flying = Animator.StringToHash("Flying");

    private readonly HashSet<int> available = new HashSet<int>();
    private SpriteRenderer sprite;
    private Animator animator;
    private EnemyBrain brain;
    private EnemyMotor motor;
    private EnemyCombat combat;
    private EnemyHealth health;

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        brain = GetComponent<EnemyBrain>();
        motor = GetComponent<EnemyMotor>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        if (animator.runtimeAnimatorController != null)
            foreach (var parameter in animator.parameters) available.Add(parameter.nameHash);
    }

    // Controllers only need the parameters their transitions use.
    private void SetBool(int hash, bool value)
    {
        if (available.Contains(hash)) animator.SetBool(hash, value);
    }

    private void Update()
    {
        bool dead = health.CurrentHealth <= 0;
        bool hurt = !dead && (brain.State == EnemyState.Stunned || brain.State == EnemyState.Knockback);
        bool windingUp = !dead && !hurt && combat.IsWindingUp;
        bool attacking = !dead && !hurt && combat.Attack.IsAttacking;

        bool flying = motor.IsFlying;
        bool moving = flying
            ? brain.State != EnemyState.Idle || motor.Velocity.sqrMagnitude > moveThreshold * moveThreshold
            : Mathf.Abs(motor.Velocity.x) > moveThreshold;

        SetBool(Dead, dead);
        SetBool(Hurt, hurt);
        SetBool(WindingUp, windingUp);
        SetBool(Attacking, attacking);
        SetBool(Moving, moving);
        SetBool(Flying, flying);

        // EnemyCombat sets flipX at windup start; don't fight it during an attack.
        if (!dead && !windingUp && !attacking && Mathf.Abs(motor.Velocity.x) > moveThreshold)
            sprite.flipX = (motor.Velocity.x < 0) == artFacesRight;
    }
}