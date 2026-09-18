using UnityEngine;

public class Projectile : MonoBehaviour
{
    private CharacterBase _target;
    private float _damage;
    private float _speed;
    private int _targetGeneration;

    public void Initialize(CharacterBase target, float damage, float speed)
    {
        _target = target;
        _targetGeneration = target != null ? target.LifeGeneration : 0;
        _damage = damage;
        _speed = speed;
    }

    private void Update()
    {
        if (_target == null || _target.IsDead || !_target.gameObject.activeInHierarchy || _target.LifeGeneration != _targetGeneration)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            _target.transform.position,
            _speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, _target.transform.position) <= 0.1f)
        {
            _target.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
