using System.Collections;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private string _playerPrefabKey = "UI/Prefabs/Player";
    [SerializeField] private string _monsterPrefabKey = "UI/Prefabs/Monster";
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private Transform _monsterSpawnPoint;
    [SerializeField] private AStarGrid _grid;
    [SerializeField] private RectTransform _characterRoot;
    [SerializeField] private Vector2 _defaultPlayerPosition = new(-150f, 0f);
    [SerializeField] private Vector2 _defaultMonsterPosition = new(150f, 0f);
    [SerializeField] private float _monsterRespawnDelay = 2f;
    [SerializeField] private int _clearMonsterCount = 3;

    public Player Player { get; private set; }
    public Monster Monster { get; private set; }
    public int DefeatedMonsterCount { get; private set; }
    public bool IsStageCleared { get; private set; }

    private Coroutine _monsterRespawnCoroutine;

    private void Start()
    {
        pageStage stagePage = UIManager.Instance.OpenPage<pageStage>();
        if (stagePage != null && stagePage.CharacterRoot != null)
            _characterRoot = stagePage.CharacterRoot;

        CreatePlayer();
        stagePage?.BindPlayer(Player);
        CreateMonster();
    }

    private void CreatePlayer()
    {
        GameObject prefab = CResourceManager.Instance.Load<GameObject>(_playerPrefabKey);
        if (prefab == null)
            return;

        GameObject playerObject = Instantiate(
            prefab,
            GetSpawnPosition(_playerSpawnPoint, _defaultPlayerPosition),
            GetSpawnRotation(_playerSpawnPoint),
            _characterRoot);
        playerObject.SetActive(true);
        Player = playerObject.GetComponent<Player>();
        if (Player == null)
        {
            Debug.LogError($"[StageManager] Player component not found: {_playerPrefabKey}");
            Destroy(playerObject);
            return;
        }

        PlayerInputMovement movement = Player.GetComponent<PlayerInputMovement>();
        movement?.SetMovementBounds(_characterRoot);
    }

    private void CreateMonster()
    {
        Monster = CObjectPoolManager.Instance.Get<Monster>(
            _monsterPrefabKey,
            GetSpawnPosition(_monsterSpawnPoint, _defaultMonsterPosition),
            GetSpawnRotation(_monsterSpawnPoint),
            _characterRoot);

        if (Monster == null)
            return;

        AStarGrid grid = _grid != null ? _grid : FindAnyObjectByType<AStarGrid>();
        MonsterAStarMovement movement = Monster.GetComponent<MonsterAStarMovement>();
        movement?.SetGrid(grid);
    }

    public void HandleMonsterDefeated(Monster deadMonster)
    {
        if (deadMonster == null || IsStageCleared)
            return;

        DefeatedMonsterCount++;

        if (DefeatedMonsterCount >= _clearMonsterCount)
        {
            ClearStage(deadMonster);
            return;
        }

        RespawnMonster(deadMonster);
    }

    public void RestartStage()
    {
        IsStageCleared = false;
        DefeatedMonsterCount = 0;

        if (_monsterRespawnCoroutine != null)
        {
            StopCoroutine(_monsterRespawnCoroutine);
            _monsterRespawnCoroutine = null;
        }

        if (Monster != null)
        {
            Monster.GetComponent<CPoolObject>()?.Release();
            Monster = null;
        }

        if (Player != null)
        {
            Destroy(Player.gameObject);
            Player = null;
        }

        pageStage stagePage = FindAnyObjectByType<pageStage>();
        if (stagePage == null && UIManager.Instance != null)
            stagePage = UIManager.Instance.OpenPage<pageStage>();

        CreatePlayer();
        stagePage?.BindPlayer(Player);
        CreateMonster();
    }

    public void RespawnMonster(Monster deadMonster)
    {
        if (deadMonster == null || deadMonster != Monster || _monsterRespawnCoroutine != null)
            return;

        Monster = null;
        deadMonster.GetComponent<CPoolObject>()?.Release();
        _monsterRespawnCoroutine = StartCoroutine(RespawnMonsterAfterDelay());
    }

    private void ClearStage(Monster deadMonster)
    {
        IsStageCleared = true;
        Monster = null;
        deadMonster.GetComponent<CPoolObject>()?.Release();

        if (UIManager.Instance != null)
            UIManager.Instance.OpenPopup<PopupResult>("popupResult");
    }

    private IEnumerator RespawnMonsterAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, _monsterRespawnDelay));
        CreateMonster();
        _monsterRespawnCoroutine = null;
    }

    private Vector3 GetSpawnPosition(Transform spawnPoint, Vector2 defaultPosition)
    {
        if (spawnPoint != null)
            return spawnPoint.position;

        if (_characterRoot != null)
            return _characterRoot.TransformPoint(defaultPosition);

        return defaultPosition;
    }

    private static Quaternion GetSpawnRotation(Transform spawnPoint)
    {
        return spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
    }
}
