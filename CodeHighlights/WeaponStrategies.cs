using System;
using System.Collections.Generic;
using UnityEngine;

public struct WeaponStats
{
    // The battle uses Canvas coordinates; matches monster speed conversion.
    public const float Units = 120f;
    public float Damage, Cooldown, Radius, Duration;
    public int Count, Pierce;
    public string IconPath, AttackType;
}

public interface IWeaponStrategy
{
    bool Execute(Player owner, WeaponStats stats);
}

public sealed class WeaponRuntime
{
    private readonly IWeaponStrategy _strategy;
    private float _remaining;
    public WeaponRuntime(IWeaponStrategy strategy) { _strategy = strategy; }
    public void Tick(Player owner, WeaponStats stats, float deltaTime)
    {
        _remaining -= deltaTime;
        if (_remaining <= 0f && _strategy.Execute(owner, stats)) _remaining = stats.Cooldown;
    }
}

public static class WeaponStrategyFactory
{
    private static readonly Dictionary<string, Func<IWeaponStrategy>> Factories = new() {
        { "MELEE_ARC", () => new MeleeArcStrategy() },
        { "ORBIT", () => new OrbitStrategy() },
        { "HOMING_PROJECTILE", () => new HomingProjectileStrategy() },
        { "RADIAL_BURST", () => new RadialBurstStrategy() },
        { "BOUNCE_PROJECTILE", () => new BounceProjectileStrategy() },
        { "TRAIL_ZONE", () => new TrailZoneStrategy() }
    };
    public static bool Supports(string type) => Factories.ContainsKey(type);
    public static IWeaponStrategy Create(string type) => Factories[type]();
}

public static class WeaponTargets
{
    // Snapshot: damage can remove/pool monsters during iteration.
    public static List<Monster> Alive(Player owner)
    {
        var stage = UnityEngine.Object.FindAnyObjectByType<StageManager>();
        var result = new List<Monster>();
        if (stage != null)
        {
            foreach (var m in stage.ActiveMonsters)
                if (m != null && !m.IsDead && m.gameObject.activeInHierarchy) result.Add(m);
        }
        else
        {
            foreach (var m in UnityEngine.Object.FindObjectsByType<Monster>())
                if (!m.IsDead && m.gameObject.activeInHierarchy) result.Add(m);
        }
        result.Sort((a,b) => ((Vector2)(a.transform.localPosition - owner.transform.localPosition)).sqrMagnitude.CompareTo(
            ((Vector2)(b.transform.localPosition - owner.transform.localPosition)).sqrMagnitude));
        return result;
    }
}

public sealed class MeleeArcStrategy : IWeaponStrategy
{
    public bool Execute(Player owner, WeaponStats stats)
    {
        var targets = WeaponTargets.Alive(owner);
        if (targets.Count == 0) return false;
        Vector2 origin = owner.transform.localPosition;
        Vector2 forward = ((Vector2)targets[0].transform.localPosition - origin).normalized;
        bool hit = false;
        foreach (var target in targets)
        {
            Vector2 offset = (Vector2)target.transform.localPosition - origin;
            if (offset.magnitude > stats.Radius || Vector2.Dot(forward, offset.normalized) < 0f) continue;
            target.TakeDamage(stats.Damage); hit = true;
        }
        if (hit)
        {
            var effect = WeaponEffect.Spawn(owner, stats, WeaponEffectKind.Flash, origin + forward * stats.Radius * 0.5f, new Color(1f, 0.75f, 0.25f, 0.35f));
            effect.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
        }
        return hit;
    }
}

public sealed class RadialBurstStrategy : IWeaponStrategy
{
    public bool Execute(Player owner, WeaponStats stats)
    {
        bool hit = false;
        foreach (var target in WeaponTargets.Alive(owner))
        {
            if (Vector2.Distance(owner.transform.localPosition, target.transform.localPosition) > stats.Radius) continue;
            target.TakeDamage(stats.Damage); hit = true;
        }
        if (hit) WeaponEffect.Spawn(owner, stats, WeaponEffectKind.Flash, owner.transform.localPosition, new Color(1f, 0.4f, 0.1f, 0.3f));
        return hit;
    }
}

public sealed class HomingProjectileStrategy : IWeaponStrategy
{
    public bool Execute(Player owner, WeaponStats stats) => Fire(owner, stats, false);
    internal static bool Fire(Player owner, WeaponStats stats, bool bounce)
    {
        var targets = WeaponTargets.Alive(owner);
        if (targets.Count == 0) return false;
        for (int i = 0; i < stats.Count; i++)
        {
            var effect = WeaponEffect.Spawn(owner, stats, bounce ? WeaponEffectKind.Bounce : WeaponEffectKind.Projectile,
                (Vector2)owner.transform.localPosition + Vector2.up * ((i - (stats.Count - 1) * 0.5f) * 20f), bounce ? Color.cyan : Color.yellow);
            effect.SetTarget(targets[i % targets.Count]);
        }
        return true;
    }
}

public sealed class BounceProjectileStrategy : IWeaponStrategy
{
    public bool Execute(Player owner, WeaponStats stats) => HomingProjectileStrategy.Fire(owner, stats, true);
}

public sealed class OrbitStrategy : IWeaponStrategy
{
    private readonly List<WeaponEffect> _orbs = new();
    public bool Execute(Player owner, WeaponStats stats)
    {
        _orbs.RemoveAll(effect => effect == null);
        while (_orbs.Count < stats.Count)
            _orbs.Add(WeaponEffect.Spawn(owner, stats, WeaponEffectKind.Orbit, owner.transform.localPosition, Color.magenta));
        for (int i = 0; i < _orbs.Count; i++) _orbs[i].ConfigureOrbit(stats, i * 360f / _orbs.Count);
        return true;
    }
}

public sealed class TrailZoneStrategy : IWeaponStrategy
{
    public bool Execute(Player owner, WeaponStats stats)
    {
        WeaponEffect.Spawn(owner, stats, WeaponEffectKind.Zone, owner.transform.localPosition, new Color(1f, 0.8f, 0.9f, 0.4f));
        return true;
    }
}
