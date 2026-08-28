using UnityEngine;

public abstract class CharacterState
{
    protected CharacterBase Owner { get; private set; }

    public void Initialize(CharacterBase owner)
    {
        Owner = owner;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void Exit() { }
}

public sealed class CharacterStateMachine
{
    private readonly CharacterBase _owner;

    public CharacterState CurrentState { get; private set; }

    public CharacterStateMachine(CharacterBase owner)
    {
        _owner = owner;
    }

    public void ChangeState(CharacterState nextState)
    {
        if (nextState == null || nextState == CurrentState)
            return;

        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Initialize(_owner);
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}

public sealed class CharacterIdleState : CharacterState
{
    public override void Update()
    {
        CharacterBase target = Owner.FindTarget();
        if (target != null && Owner.ShouldAttack(target))
            Owner.BeginAttack(target);
    }
}

public sealed class CharacterAttackState : CharacterState
{
    private float _elapsed;

    public override void Enter()
    {
        _elapsed = 0f;
        Owner.PerformAttack();
    }

    public override void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= Owner.AttackInterval)
            Owner.ChangeToIdleState();
    }
}

public sealed class CharacterDieState : CharacterState
{
    public override void Enter()
    {
        Owner.HandleDeath();
    }
}
