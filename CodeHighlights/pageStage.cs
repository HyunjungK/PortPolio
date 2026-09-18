using UnityEngine;
using UnityEngine.UI;

public class pageStage : UIPage
{
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Text hpText;
    [SerializeField] private Slider expSlider;
    [SerializeField] private Text expText;
    [SerializeField] private RectTransform characterRoot;
    [SerializeField] private StageCameraFollow cameraFollow;
    [SerializeField] private Text timerText;
    [SerializeField] private Text monsterCountText;
    [SerializeField] private Text levelText;

    private Player _player;
    private StageManager _stageManager;

    public RectTransform CharacterRoot
    {
        get
        {
            if (characterRoot == null)
                characterRoot = FindRectTransform("BattleWorld");
            return characterRoot;
        }
    }

    protected override void OnOpened()
    {
        cameraFollow ??= GetComponent<StageCameraFollow>();
        if (cameraFollow != null)
            cameraFollow.Initialize(CharacterRoot, transform as RectTransform);

        BindStageManager(FindAnyObjectByType<StageManager>());
        BindPlayer(FindAnyObjectByType<Player>());
    }

    protected override void OnClosed()
    {
        UnbindPlayer();
        UnbindStageManager();
    }

    public void BindStageManager(StageManager stageManager)
    {
        if (_stageManager == stageManager)
        {
            RefreshStageHud();
            return;
        }

        UnbindStageManager();
        _stageManager = stageManager;
        if (_stageManager == null)
            return;

        _stageManager.StageTimeChanged += OnStageTimeChanged;
        _stageManager.MonsterCountChanged += OnMonsterCountChanged;
        RefreshStageHud();
    }

    public void BindPlayer(Player player)
    {
        if (_player == player)
        {
            RefreshSliders();
            cameraFollow?.Bind(_player);
            return;
        }

        UnbindPlayer();
        _player = player;
        cameraFollow?.Bind(_player);
        if (_player == null)
            return;

        _player.HealthChanged += OnHealthChanged;
        _player.ExperienceChanged += OnExperienceChanged;
        RefreshSliders();
    }

    private void UnbindStageManager()
    {
        if (_stageManager == null)
            return;

        _stageManager.StageTimeChanged -= OnStageTimeChanged;
        _stageManager.MonsterCountChanged -= OnMonsterCountChanged;
        _stageManager = null;
    }

    private void UnbindPlayer()
    {
        if (_player == null)
            return;

        _player.HealthChanged -= OnHealthChanged;
        _player.ExperienceChanged -= OnExperienceChanged;
        _player = null;
        cameraFollow?.Bind(null);
    }

    private void OnStageTimeChanged(float remainingTime, float duration)
    {
        if (timerText == null)
            return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void OnMonsterCountChanged(int defeatedCount, int targetCount)
    {
        if (monsterCountText == null)
            return;

        monsterCountText.text = targetCount > 0 ? $"KILL {defeatedCount}/{targetCount}" : $"KILL {defeatedCount}";
    }

    private void OnHealthChanged(float health, float maxHealth)
    {
        if (hpSlider == null)
            return;

        hpSlider.minValue = 0f;
        hpSlider.maxValue = maxHealth;
        hpSlider.value = health;

        hpText.text=$"HP {health} / {maxHealth}";
    }

    private void OnExperienceChanged(int experience, int experienceToNextLevel)
    {
        if (expSlider == null)
            return;

        expSlider.minValue = 0f;
        expSlider.maxValue = experienceToNextLevel;
        expSlider.value = experience;

        expText.text=$"EXP {experience}%";

        levelText.text=$"Baker Lv.{_player.Level}";
    }

    private void RefreshSliders()
    {
        if (_player == null)
            return;

        OnHealthChanged(_player.Health, _player.MaxHealth);
        OnExperienceChanged(_player.Experience, _player.ExperienceToNextLevel);
    }

    private void RefreshStageHud()
    {
        if (_stageManager == null)
            return;

        OnStageTimeChanged(_stageManager.RemainingTime, _stageManager.Duration);
        OnMonsterCountChanged(_stageManager.DefeatedMonsterCount, 0);
    }

    private RectTransform FindRectTransform(string objectName)
    {
        foreach (RectTransform rectTransform in GetComponentsInChildren<RectTransform>(true))
        {
            if (rectTransform.name == objectName)
                return rectTransform;
        }

        return null;
    }
}
