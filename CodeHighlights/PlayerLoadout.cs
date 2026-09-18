using System.Collections.Generic;
using UnityEngine;

public sealed class LevelupChoice
{
    public string Id { get; }
    public bool IsWeapon { get; }
    public int Level { get; }
    public string Name { get; }
    public string Description { get; }
    public string IconPath { get; }
    public LevelupChoice(string id, bool weapon, int level, string name, string description, string iconPath = null)
    { Id = id; IsWeapon = weapon; Level = level; Name = name; Description = description; IconPath = iconPath; }
}

// Per-run state. CSV definitions are never mutated by upgrades.
public sealed class PlayerLoadout
{
    private readonly Player _owner;
    private readonly Dictionary<string, int> _weapons = new();
    private readonly Dictionary<string, int> _passives = new();
    private readonly Dictionary<string, WeaponRuntime> _runtime = new();
    private readonly float _baseDamage, _baseInterval, _baseHealth, _baseSpeed;
    private float _levelHealthBonus;
    public int Chapter { get; set; } = 1;
    public float DamageMultiplier => 1f + Bonus("DAMAGE_MULTIPLIER");
    public float CooldownMultiplier => Mathf.Max(0.1f, 1f - Bonus("COOLDOWN_REDUCTION"));
    public float AreaMultiplier => 1f + Bonus("AREA_MULTIPLIER");
    public float DurationMultiplier => 1f + Bonus("DURATION_MULTIPLIER");
    public int ProjectileBonus => Mathf.RoundToInt(Bonus("PROJECTILE_BONUS"));

    public PlayerLoadout(Player owner)
    {
        _owner = owner;
        _baseDamage = owner.AttackDamage; _baseInterval = owner.AttackInterval;
        _baseHealth = owner.MaxHealth; _baseSpeed = owner.Movement != null ? owner.Movement.CurrentMoveSpeed : 0f;
    }

    public int GetLevel(string id, bool weapon) => (weapon ? _weapons : _passives).TryGetValue(id, out int level) ? level : 0;
    private float Bonus(string stat)
    {
        float value = 0f;
        foreach (var entry in _passives)
        {
            PassiveData data = CDataManager.Instance.Passives.GetPassiveData(entry.Key);
            if (data != null && data.stat_type == stat)
                value += data.value_per_level * entry.Value;
        }
        return value;
    }

    private static bool SupportsPassive(string stat) => stat == "DAMAGE_MULTIPLIER" || stat == "COOLDOWN_REDUCTION" ||
        stat == "PROJECTILE_BONUS" || stat == "AREA_MULTIPLIER" || stat == "DURATION_MULTIPLIER" ||
        stat == "MOVE_SPEED_MULTIPLIER" || stat == "MAX_HP_MULTIPLIER";

    public List<LevelupChoice> GetAvailableChoices()
    {
        var choices = new List<LevelupChoice>();
        AddWeaponChoices(choices);
        AddPassiveChoices(choices);
        return choices;
    }

    private void AddWeaponChoices(List<LevelupChoice> choices)
    {
        int slots = CDataManager.Instance.GameConfig.GetIntValue("STAGE_MAX_WEAPON_SLOTS", 6);
        foreach (WeaponData data in CDataManager.Instance.Weapons.GetWeaponDataAll())
        {
            int current = GetLevel(data.id, true);
            if (data.unlock_chapter > Chapter || current >= data.max_level) continue;
            if (current == 0 && _weapons.Count >= slots) continue;
            if (!WeaponStrategyFactory.Supports(data.attack_type) ||
                CDataManager.Instance.WeaponLevels.GetWeaponLevelData(data.id, current + 1) == null) continue;

            WeaponStats stats = GetWeaponStats(data.id, current + 1);
            string description = data.description +
                $"\n피해 {stats.Damage:0.#} · {stats.Cooldown:0.##}초\n범위 {stats.Radius / WeaponStats.Units:0.##} · 개수 {stats.Count}";
            choices.Add(new LevelupChoice(data.id, true, current + 1, data.name, description, data.res));
        }
    }

    private void AddPassiveChoices(List<LevelupChoice> choices)
    {
        int slots = CDataManager.Instance.GameConfig.GetIntValue("STAGE_MAX_PASSIVE_SLOTS", 6);
        foreach (PassiveData data in CDataManager.Instance.Passives.GetPassiveDataAll())
        {
            int current = GetLevel(data.id, false);
            if (data.unlock_chapter > Chapter || current >= data.max_level) continue;
            if (current == 0 && _passives.Count >= slots) continue;
            if (!SupportsPassive(data.stat_type)) continue;

            float amount = data.value_per_level;
            bool count = data.stat_type == "PROJECTILE_BONUS";
            string description = data.description + (count ?
                $"\n추가 개수 {current * amount:0} → {(current + 1) * amount:0}" :
                $"\n보너스 {current * amount:P0} → {(current + 1) * amount:P0}");
            choices.Add(new LevelupChoice(data.id, false, current + 1, data.name, description));
        }
    }

    public bool TryApply(LevelupChoice choice)
    {
        if (choice == null || _owner.IsDead) return false;
        bool valid = GetAvailableChoices().Exists(c => c.Id == choice.Id && c.IsWeapon == choice.IsWeapon && c.Level == choice.Level);
        if (!valid) return false;
        (choice.IsWeapon ? _weapons : _passives)[choice.Id] = choice.Level;
        if (choice.IsWeapon && !_runtime.ContainsKey(choice.Id))
        {
            WeaponData data = CDataManager.Instance.Weapons.GetWeaponData(choice.Id);
            _runtime.Add(choice.Id, new WeaponRuntime(WeaponStrategyFactory.Create(data.attack_type)));
        }
        ApplyStats();
        return true;
    }

    public void ApplyLevelupGrowth()
    {
        _levelHealthBonus += Mathf.Max(0, CDataManager.Instance.GameConfig.GetIntValue("LEVELUP_MAX_HP_GAIN", 10));
        ApplyStats();
        _owner.RestoreFullHealth();
    }

    private void ApplyStats()
    {
        _owner.ApplyCombatStats((_baseHealth + _levelHealthBonus) * (1f + Bonus("MAX_HP_MULTIPLIER")), _baseDamage * DamageMultiplier, _baseInterval * CooldownMultiplier);
        _owner.Movement?.SetMoveSpeed(_baseSpeed * (1f + Bonus("MOVE_SPEED_MULTIPLIER")));
    }

    public WeaponStats GetWeaponStats(string id, int level)
    {
        WeaponData data = CDataManager.Instance.Weapons.GetWeaponData(id);
        WeaponLevelData upgrade = CDataManager.Instance.WeaponLevels.GetWeaponLevelData(id, level);
        return new WeaponStats {
            IconPath = data?.res,
            AttackType = data?.attack_type,
            Damage = (data?.base_damage ?? 0f) * (upgrade?.damage_multiplier ?? 1f) * DamageMultiplier,
            Cooldown = Mathf.Max(0.05f, (data?.base_cooldown_sec ?? 1f) * (upgrade?.cooldown_multiplier ?? 1f) * CooldownMultiplier),
            Radius = (data?.base_area ?? 1f) * (upgrade?.area_multiplier ?? 1f) * AreaMultiplier * WeaponStats.Units,
            Count = Mathf.Max(1, (data?.base_projectiles ?? 1) + (upgrade?.projectile_bonus ?? 0) + ProjectileBonus),
            Pierce = upgrade?.pierce_bonus ?? 0,
            Duration = 2f * (upgrade?.duration_multiplier ?? 1f) * DurationMultiplier
        };
    }

    public void Tick(float deltaTime)
    {
        foreach (var entry in _runtime)
        {
            if (Time.timeScale <= 0f || _owner.IsDead) break;
            entry.Value.Tick(_owner, GetWeaponStats(entry.Key, _weapons[entry.Key]), deltaTime);
        }
    }
}
