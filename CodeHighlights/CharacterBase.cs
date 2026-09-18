using System;
using UnityEngine;

public abstract class CharacterBase : MonoBehaviour
{
    [Header("Character")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _attackDamage = 10f;
    [SerializeField] private float _attackInterval = 1f;
    [SerializeField] private CharacterMovement _movement;

    private float _health;
    private CharacterAnimationBridge _animationBridge;
    private CharacterFacing _facing;
    private float _nextAttackTime;

    public CharacterStateMachine StateMachine { get; private set; }
    public float Health => _health;
    public float MaxHealth => _maxHealth;
    public float AttackDamage => _attackDamage;
    public float AttackInterval => _attackInterval;
    public int LifeGeneration { get; private set; }
    public bool IsDead { get; private set; }
    public CharacterBase Target { get; private set; }
    public CharacterMovement Movement => _movement;
    internal bool IsAttacking => StateMachine?.CurrentState is CharacterAttackState;
    public event Action<float, float> HealthChanged;

    protected virtual void Awake()
    {
        StateMachine = new CharacterStateMachine(this);
        _movement ??= GetComponent<CharacterMovement>();
        _animationBridge ??= GetComponentInChildren<CharacterAnimationBridge>();
        _facing ??= GetComponent<CharacterFacing>();
        _movement?.Initialize(this);
        ResetCharacter();
    }

    protected virtual void OnEnable()
    {
        if (StateMachine.CurrentState != null)
            ResetCharacter();
    }

    protected virtual void Update()
    {
        if (Time.deltaTime <= 0f) return;
        if (!IsDead)
        {
            StateMachine.Update();
            _movement?.Tick(Time.deltaTime);
        }
    }

    public void ConfigureStats(float maxHealth, float attackDamage, float attackInterval)
    {
        _maxHealth = Mathf.Max(1f, maxHealth);
        _attackDamage = Mathf.Max(0f, attackDamage);
        _attackInterval = Mathf.Max(0.05f, attackInterval);
        ResetCharacter();
    }

    public void ResetCharacter()
    {
        LifeGeneration++;
        _health = Mathf.Max(0f, _maxHealth);
        _nextAttackTime = 0f;
        IsDead = _health <= 0f;
        Target = null;
        HealthChanged?.Invoke(_health, _maxHealth);

        if (IsDead)
            StateMachine.ChangeState(new CharacterDieState());
        else
            ChangeToIdleState();
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return;

        _health = Mathf.Max(0f, _health - damage);
        HealthChanged?.Invoke(_health, _maxHealth);
        if (_health > 0f)
            _animationBridge?.PlayHit();

        if (_health <= 0f)
        {
            IsDead = true;
            StateMachine.ChangeState(new CharacterDieState());
        }
    }

    // Upgrade stats without resetting health, attack timers, target or state.
    public void ApplyCombatStats(float maxHealth, float attackDamage, float attackInterval)
    {
        float previousMax = _maxHealth;
        _maxHealth = Mathf.Max(1f, maxHealth);
        _attackDamage = Mathf.Max(0f, attackDamage);
        _attackInterval = Mathf.Max(0.05f, attackInterval);
        _health = Mathf.Clamp(_health + Mathf.Max(0f, _maxHealth - previousMax), 0f, _maxHealth);
        HealthChanged?.Invoke(_health, _maxHealth);
    }

    public void RestoreFullHealth()
    {
        if (IsDead) return;
        _health = _maxHealth;
        HealthChanged?.Invoke(_health, _maxHealth);
    }

    public bool TryAttack(CharacterBase target)
    {
        if (IsDead || target == null || target.IsDead || Time.time < _nextAttackTime)
            return false;

        Target = target;
        StateMachine.ChangeState(new CharacterAttackState());
        return true;
    }

    internal void BeginAttack(CharacterBase target)
    {
        TryAttack(target);
    }

    internal virtual void PerformAttack()
    {
        StartAttackCooldown();
        if (Target != null && !Target.IsDead)
            Target.TakeDamage(_attackDamage);

        OnAttack(Target);
    }

    protected void StartAttackCooldown()
    {
        _nextAttackTime = Time.time + Mathf.Max(0.01f, _attackInterval);
    }

    internal void ChangeToIdleState()
    {
        if (!IsDead)
            StateMachine.ChangeState(new CharacterIdleState());
    }

    internal void PlayIdleAnimation()
    {
        _animationBridge?.PlayIdle();
    }

    internal void PlayAttackAnimation()
    {
        _animationBridge?.PlayAttack();
    }

    internal void FaceTarget()
    {
        if (Target == null)
            return;

        _facing ??= GetComponent<CharacterFacing>();
        _facing?.FaceDirection(Target.transform.position - transform.position);
    }

    internal void HandleDeath()
    {
        Target = null;
        _movement?.Stop();
        OnDie();
    }

    internal virtual CharacterBase FindTarget() => null;
    internal virtual bool ShouldAttack(CharacterBase target) => false;
    protected virtual void OnAttack(CharacterBase target) { }
    protected virtual void OnDie() { }
}
