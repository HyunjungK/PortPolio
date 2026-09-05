using UnityEngine;

public class StageCameraFollow : MonoBehaviour
{
    [SerializeField] private RectTransform _worldRoot;
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private Vector2 _worldSize = new(2600f, 1460f);
    [SerializeField] private float _followDamping = 12f;

    private RectTransform _target;

    public RectTransform WorldRoot => _worldRoot;

    public void Initialize(RectTransform worldRoot, RectTransform viewport)
    {
        _worldRoot = worldRoot;
        _viewport = viewport;
        if (_worldRoot != null)
            _worldRoot.sizeDelta = _worldSize;
    }

    public void Bind(Player player)
    {
        _target = player != null ? player.transform as RectTransform : null;
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (_target == null)
        {
            Player player = FindAnyObjectByType<Player>();
            _target = player != null ? player.transform as RectTransform : null;
            if (_target == null)
                return;
        }

        Follow(Time.unscaledDeltaTime);
    }

    public void SnapToTarget()
    {
        if (_worldRoot == null || _target == null)
            return;

        _worldRoot.anchoredPosition = GetClampedCameraOffset();
    }

    void Follow(float deltaTime)
    {
        if (_worldRoot == null)
            return;

        Vector2 desired = GetClampedCameraOffset();
        float t = 1f - Mathf.Exp(-_followDamping * Mathf.Max(0f, deltaTime));
        _worldRoot.anchoredPosition = Vector2.Lerp(_worldRoot.anchoredPosition, desired, t);
    }

    Vector2 GetClampedCameraOffset()
    {
        Vector2 desired = -_target.anchoredPosition;
        Rect viewportRect = _viewport != null ? _viewport.rect : new Rect(0f, 0f, 1920f, 1080f);
        Vector2 maxOffset = new(
            Mathf.Max(0f, (_worldSize.x - viewportRect.width) * 0.5f),
            Mathf.Max(0f, (_worldSize.y - viewportRect.height) * 0.5f));

        desired.x = Mathf.Clamp(desired.x, -maxOffset.x, maxOffset.x);
        desired.y = Mathf.Clamp(desired.y, -maxOffset.y, maxOffset.y);
        return desired;
    }
}
