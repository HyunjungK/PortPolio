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
    private float _nextAttackTime;

    public CharacterStateMachine StateMachine { get; private set; }
    public float Health => _health;
    public float MaxHealth => _maxHealth;
    public float AttackDamage => _attackDamage;
    public float AttackInterval => _attackInterval;
    public bool IsDead { get; private set; }
    public CharacterBase Target { get; private set; }
    public CharacterMovement Movement => _movement;
    public event Action<float, float> HealthChanged;

    protected virtual void Awake()
    {
        StateMachine = new CharacterStateMachine(this);
        _movement ??= GetComponent<CharacterMovement>();
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
        if (!IsDead)
        {
            StateMachine.Update();
            _movement?.Tick(Time.deltaTime);
        }
    }

    public void ResetCharacter()
    {
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
        if (_health <= 0f)
        {
            IsDead = true;
            StateMachine.ChangeState(new CharacterDieState());
        }
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
