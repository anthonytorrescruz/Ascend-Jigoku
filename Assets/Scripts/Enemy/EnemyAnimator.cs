using System.Collections.Generic;
using UnityEngine;

// Drives the enemy's Animator bool parameters (Moving, WindingUp, Attacking, Hurt, Dead, Flying) from brain, motor and
// combat state, and flips the sprite to face the target or direction of travel. Parameters missing from the controller are skipped.
[RequireComponent(typeof(SpriteRenderer), typeof(Animator), typeof(EnemyBrain))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]   // after EnemyCombat, which assumes right-facing art when it flips at windup
public sealed class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private bool artFacesRight = true;
    [Min(0)] [SerializeField] private float moveThreshold = 0.1f;
    [Min(0)] [SerializeField] private float turnThreshold = 0.5f;

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

        // Face the target while attacking, otherwise the way it's moving. The dead zone (turnThreshold) stops hovering
        // flyers flip-flopping.
        float faceX = windingUp || attacking
            ? (combat.Target != null ? combat.Target.transform.position.x - transform.position.x : 0)
            : motor.Velocity.x;
        if (!dead && !hurt && Mathf.Abs(faceX) > turnThreshold)
            sprite.flipX = (faceX < 0) == artFacesRight;
    }
}