using UnityEngine;

public sealed class CharacterAnimationBridge : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    static readonly int AttackHash = Animator.StringToHash("Attack");
    static readonly int HitHash = Animator.StringToHash("Hit");

    void Awake()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    public void PlayIdle()
    {
        if (_animator == null)
            return;

        _animator.CrossFade("Idle", 0.08f, 0);
    }

    public void PlayAttack()
    {
        if (_animator == null)
            return;

        _animator.ResetTrigger(HitHash);
        _animator.SetTrigger(AttackHash);
    }

    public void PlayHit()
    {
        if (_animator == null)
            return;

        _animator.ResetTrigger(AttackHash);
        _animator.SetTrigger(HitHash);
    }
}
