using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    private sealed class RuntimeWave
    {
        public StageWaveData WaveData;
        public CsvRow MonsterRow;
        public string MonsterId;
        public float StartSec;
        public float EndSec;
        public float SpawnInterval;
        public int MaxAlive;
        public int AliveCount;
        public float NextSpawnTime;
    }

    [Header("Stage")]
    [SerializeField] private int _stageId = 1;
    [SerializeField] private int _stageLevel = 1;
    [SerializeField] private float _fallbackStageDuration = 180f;
    [SerializeField] private bool _useClearMonsterCount = false;
    [SerializeField] private int _clearMonsterCount = 30;

    [Header("Spawn")]
    [SerializeField] private string _playerPrefabKey = "UI/Prefabs/Player";
    [SerializeField] private string _monsterPrefabKey = "UI/Prefabs/Monster";
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private Transform _monsterSpawnPoint;
    [SerializeField] private AStarGrid _grid;
    [SerializeField] private RectTransform _characterRoot;
    [SerializeField] private Vector2 _defaultPlayerPosition = new(-150f, 0f);
    [SerializeField] private Vector2 _defaultMonsterPosition = new(150f, 0f);
    [SerializeField] private int _initialMonsterCount = 6;
    [SerializeField] private int _maxActiveMonsterCount = 18;
    [SerializeField] private int _prewarmMonsterCount = 18;
    [SerializeField] private float _spawnMinDistanceFromPlayer = 260f;
    [SerializeField] private float _spawnMaxDistanceFromPlayer = 520f;

    public Player Player { get; private set; }
    public Monster Monster => _activeMonsters.Count > 0 ? _activeMonsters[0] : null;
    public IReadOnlyList<Monster> ActiveMonsters => _activeMonsters;
    public int DefeatedMonsterCount { get; private set; }
    public bool IsStageCleared { get; private set; }
    public float ElapsedTime => _elapsedTime;
    public float Duration => _duration;
    public float RemainingTime => Mathf.Max(0f, _duration - _elapsedTime);

    public event Action<float, float> StageTimeChanged;
    public event Action<int, int> MonsterCountChanged;

    private readonly List<RuntimeWave> _waves = new();
    private readonly List<Monster> _activeMonsters = new();
    private readonly Dictionary<Monster, RuntimeWave> _monsterWaveMap = new();
    private bool _isRunning;
    private bool _resultOpened;
    private float _elapsedTime;
    private float _duration;

    private void Start()
    {
        CDataManager.Instance.LoadAll();

        pageStage stagePage = UIManager.Instance.OpenPage<pageStage>();
        if (stagePage != null && stagePage.CharacterRoot != null)
            _characterRoot = stagePage.CharacterRoot;

        stagePage?.BindStageManager(this);
        StartStage();
    }

    private void Update()
    {
        if (!_isRunning || IsStageCleared)
            return;

        _elapsedTime = Mathf.Min(_duration, _elapsedTime + Time.deltaTime);
        StageTimeChanged?.Invoke(RemainingTime, _duration);

        SpawnWaveMonsters();

        if (_elapsedTime >= _duration)
            FinishStage();
    }

    public void StartStage()
    {
        IsStageCleared = false;
        _resultOpened = false;
        DefeatedMonsterCount = 0;
        _elapsedTime = 0f;

        LoadStageRuntimeData();
        CObjectPoolManager.Instance.Prewarm(_monsterPrefabKey, Mathf.Max(0, _prewarmMonsterCount));

        CreatePlayer();
        FindAnyObjectByType<pageStage>()?.BindPlayer(Player);

        SpawnInitialMonsters();
        _isRunning = true;

        StageTimeChanged?.Invoke(RemainingTime, _duration);
        MonsterCountChanged?.Invoke(DefeatedMonsterCount, _useClearMonsterCount ? _clearMonsterCount : 0);
    }

    public void RestartStage()
    {
        CleanupStageObjects();
        StartStage();
    }

    public void CleanupStageObjects()
    {
        _isRunning = false;
        _waves.Clear();
        _monsterWaveMap.Clear();

        for (int i = _activeMonsters.Count - 1; i >= 0; i--)
        {
            Monster monster = _activeMonsters[i];
            if (monster != null)
                monster.GetComponent<CPoolObject>()?.Release();
        }

        _activeMonsters.Clear();

        if (Player != null)
        {
            Destroy(Player.gameObject);
            Player = null;
        }
    }

    public void HandleMonsterDefeated(Monster deadMonster)
    {
        if (deadMonster == null || IsStageCleared)
            return;

        if (!_activeMonsters.Remove(deadMonster))
            return;

        if (_monsterWaveMap.TryGetValue(deadMonster, out RuntimeWave wave))
        {
            wave.AliveCount = Mathf.Max(0, wave.AliveCount - 1);
            _monsterWaveMap.Remove(deadMonster);
        }

        DefeatedMonsterCount++;
        deadMonster.GetComponent<CPoolObject>()?.Release();
        MonsterCountChanged?.Invoke(DefeatedMonsterCount, _useClearMonsterCount ? _clearMonsterCount : 0);

        if (_useClearMonsterCount && DefeatedMonsterCount >= _clearMonsterCount)
            FinishStage();
    }

    public void HandlePlayerDefeated(Player player)
    {
        if (player == null || player != Player || IsStageCleared)
            return;

        Player = null;
        FinishStage();
    }

    private void LoadStageRuntimeData()
    {
        _waves.Clear();
        _duration = _fallbackStageDuration;

        StageData stageData = CDataManager.Instance.Stages.GetStageData(_stageId);
        if (stageData != null)
            _duration = Mathf.Max(1f, stageData.duration_sec);

        List<StageWaveData> waveDataList = CDataManager.Instance.StageWaves.GetStageWaveDataByStageId(_stageId);
        for (int i = 0; i < waveDataList.Count; i++)
        {
            StageWaveData waveData = waveDataList[i];
            RuntimeWave wave = new RuntimeWave
            {
                WaveData = waveData,
                MonsterRow = CDataManager.Instance.Monsters.Get(waveData.monster_id),
                MonsterId = waveData.monster_id,
                StartSec = waveData.start_sec,
                EndSec = waveData.end_sec > 0f ? waveData.end_sec : _duration,
                SpawnInterval = Mathf.Max(0.1f, waveData.spawn_interval_sec),
                MaxAlive = Mathf.Max(1, waveData.max_alive),
            };

            wave.MaxAlive = Mathf.Min(wave.MaxAlive, Mathf.Max(1, _maxActiveMonsterCount));
            wave.NextSpawnTime = wave.StartSec;
            _waves.Add(wave);
        }

        if (_waves.Count == 0)
        {
            _waves.Add(new RuntimeWave
            {
                StartSec = 0f,
                EndSec = _duration,
                SpawnInterval = 1.5f,
                MaxAlive = Mathf.Max(1, _maxActiveMonsterCount),
                NextSpawnTime = 0f,
            });
        }
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
        Player.Loadout.Chapter = CDataManager.Instance.Stages.GetStageData(_stageId)?.chapter ?? 1;
        movement?.SetMovementBounds(_characterRoot);
    }

    private void SpawnInitialMonsters()
    {
        int count = Mathf.Clamp(_initialMonsterCount, 0, _maxActiveMonsterCount);
        for (int i = 0; i < count; i++)
        {
            RuntimeWave wave = GetActiveWave();
            if (wave == null)
                break;

            SpawnMonster(wave);
            wave.NextSpawnTime = Mathf.Max(wave.NextSpawnTime, _elapsedTime + wave.SpawnInterval);
        }
    }

    private void SpawnWaveMonsters()
    {
        for (int i = 0; i < _waves.Count; i++)
        {
            RuntimeWave wave = _waves[i];
            if (_elapsedTime < wave.StartSec || _elapsedTime > wave.EndSec)
                continue;

            while (_activeMonsters.Count < _maxActiveMonsterCount &&
                   wave.AliveCount < wave.MaxAlive &&
                   _elapsedTime >= wave.NextSpawnTime)
            {
                SpawnMonster(wave);
                wave.NextSpawnTime += wave.SpawnInterval;
            }
        }
    }

    private RuntimeWave GetActiveWave()
    {
        for (int i = 0; i < _waves.Count; i++)
        {
            RuntimeWave wave = _waves[i];
            if (_elapsedTime >= wave.StartSec && _elapsedTime <= wave.EndSec && wave.AliveCount < wave.MaxAlive)
                return wave;
        }

        return _waves.Count > 0 ? _waves[0] : null;
    }

    private void SpawnMonster(RuntimeWave wave)
    {
        if (wave == null || _activeMonsters.Count >= _maxActiveMonsterCount)
            return;

        Monster monster = CObjectPoolManager.Instance.Get<Monster>(
            _monsterPrefabKey,
            GetMonsterSpawnPosition(),
            GetSpawnRotation(_monsterSpawnPoint),
            _characterRoot);

        if (monster == null)
            return;

        AStarGrid grid = _grid != null ? _grid : FindAnyObjectByType<AStarGrid>();
        MonsterAStarMovement movement = monster.GetComponent<MonsterAStarMovement>();
        movement?.SetGrid(grid);

        monster.ConfigureFromData(wave.MonsterRow, _stageLevel);
        _activeMonsters.Add(monster);
        _monsterWaveMap[monster] = wave;
        wave.AliveCount++;
    }

    private void FinishStage()
    {
        if (IsStageCleared)
            return;

        IsStageCleared = true;
        _isRunning = false;
        StageTimeChanged?.Invoke(RemainingTime, _duration);
        OpenResultPopup();
    }

    private void OpenResultPopup()
    {
        if (_resultOpened || UIManager.Instance == null)
            return;

        _resultOpened = true;
        UIManager.Instance.OpenPopup<PopupResult>("popupResult");
    }

    private Vector3 GetMonsterSpawnPosition()
    {
        if (_monsterSpawnPoint != null)
            return _monsterSpawnPoint.position;

        if (Player != null)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.right;

            direction.Normalize();
            float distance = UnityEngine.Random.Range(_spawnMinDistanceFromPlayer, _spawnMaxDistanceFromPlayer);
            return Player.transform.position + (Vector3)(direction * distance);
        }

        return GetSpawnPosition(_monsterSpawnPoint, _defaultMonsterPosition);
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
