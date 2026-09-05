using UnityEngine;
using UnityEngine.UI;

public class pageStage : UIPage
{
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Slider expSlider;
    [SerializeField] private RectTransform characterRoot;
    [SerializeField] private StageCameraFollow cameraFollow;

    private Player _player;

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
        hpSlider ??= FindSlider("hpSlider");
        expSlider ??= FindSlider("expSlider");
        cameraFollow ??= GetComponent<StageCameraFollow>();
        if (cameraFollow != null)
            cameraFollow.Initialize(CharacterRoot, transform as RectTransform);
        BindPlayer(FindAnyObjectByType<Player>());
    }

    protected override void OnClosed()
    {
        UnbindPlayer();
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

    private void UnbindPlayer()
    {
        if (_player == null)
            return;

        _player.HealthChanged -= OnHealthChanged;
        _player.ExperienceChanged -= OnExperienceChanged;
        _player = null;
        cameraFollow?.Bind(null);
    }

    private void OnHealthChanged(float health, float maxHealth)
    {
        if (hpSlider == null)
            return;

        hpSlider.minValue = 0f;
        hpSlider.maxValue = maxHealth;
        hpSlider.value = health;
    }

    private void OnExperienceChanged(int experience, int experienceToNextLevel)
    {
        if (expSlider == null)
            return;

        expSlider.minValue = 0f;
        expSlider.maxValue = experienceToNextLevel;
        expSlider.value = experience;
    }

    private void RefreshSliders()
    {
        if (_player == null)
            return;

        OnHealthChanged(_player.Health, _player.MaxHealth);
        OnExperienceChanged(_player.Experience, _player.ExperienceToNextLevel);
    }

    private Slider FindSlider(string sliderName)
    {
        Transform sliderTransform = transform.Find(sliderName);
        if (sliderTransform != null)
            return sliderTransform.GetComponent<Slider>();

        foreach (Slider slider in GetComponentsInChildren<Slider>(true))
        {
            if (slider.name == sliderName)
                return slider;
        }

        return null;
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
