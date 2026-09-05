using UnityEngine;

public sealed class CharacterFacing : MonoBehaviour
{
    [SerializeField] private Transform _flipTarget;
    [SerializeField] private bool _defaultFacesRight = true;
    [SerializeField] private float _threshold = 0.01f;

    private float _baseScaleX = 1f;

    void Awake()
    {
        if (_flipTarget == null)
            _flipTarget = transform;

        _baseScaleX = Mathf.Max(0.0001f, Mathf.Abs(_flipTarget.localScale.x));
    }

    public void FaceDirection(Vector2 direction)
    {
        if (_flipTarget == null || Mathf.Abs(direction.x) < _threshold)
            return;

        bool wantsRight = direction.x > 0f;
        float sign = wantsRight == _defaultFacesRight ? 1f : -1f;
        Vector3 scale = _flipTarget.localScale;
        scale.x = _baseScaleX * sign;
        _flipTarget.localScale = scale;
    }
}
