using System;
using System.Collections.Generic;
using UnityEngine;

public enum RealmMajor { Mortal, QiRefining, Foundation, CoreFormation, NascentSoul, SpiritTransformation }
public enum RealmPhase { None, FirstSense, Early, Middle, Late, Peak, Complete }

[Serializable]
public sealed class RealmDefinition
{
    [SerializeField] string id, displayName;
    [SerializeField] RealmMajor major;
    [SerializeField] RealmPhase phase;
    [SerializeField, Min(0)] int minimumQi;
    [SerializeField, Min(0)] int fitnessBonus, strengthBonus, meridianBonus;
    [SerializeField, Min(1)] int reaction;
    [SerializeField] bool flight;

    public string Id => id;
    public string Name => displayName;
    public RealmMajor Major => major;
    public RealmPhase Phase => phase;
    public int MinimumQi => minimumQi;
    public int FitnessBonus => fitnessBonus;
    public int StrengthBonus => strengthBonus;
    public int MeridianBonus => meridianBonus;
    public int Reaction => reaction;
    public bool CanFly => flight;
}

// Derived, not serialized. Reloading or reopening the sheet cannot apply a bonus twice.
public readonly struct CultivatorAttributes
{
    public RealmDefinition Realm { get; }
    public int Fitness { get; }
    public int Strength { get; }
    public int Meridian { get; }
    public int EquipmentAttack { get; }
    public int EquipmentHealth { get; }
    public int MaxHealth => 8 + 2 * Fitness + EquipmentHealth;
    public int Attack => 2 + Strength + EquipmentAttack;
    public int ManaCapacity => 100 + 20 * Meridian;
    public int Movement => 3;
    public int Reaction => Realm.Reaction;
    public bool CanFly => Realm.CanFly;

    internal CultivatorAttributes(RealmDefinition realm, int foundationQuality, CultivationItem weapon, CultivationItem clothing,
        CultivationItem accessory)
    {
        Realm = realm;
        Fitness = 2 + realm.FitnessBonus;
        Strength = 2 + realm.StrengthBonus;
        int qualityBonus = realm.Major == RealmMajor.Foundation ? Mathf.Clamp(foundationQuality - 1, 0, 2) : 0;
        Meridian = realm.MeridianBonus + qualityBonus;
        EquipmentAttack = (weapon?.AttackBonus ?? 0) + (clothing?.AttackBonus ?? 0) + (accessory?.AttackBonus ?? 0);
        EquipmentHealth = (weapon?.HealthBonus ?? 0) + (clothing?.HealthBonus ?? 0) + (accessory?.HealthBonus ?? 0);
    }
}

[CreateAssetMenu(menuName = "青岚/境界与灵根")]
public sealed class CultivationRealms : ScriptableObject
{
    const string ResourceName = "QinglanRealms";
    static CultivationRealms table;

    [SerializeField] RealmDefinition[] stages;
    [SerializeField] SpiritRoots mixedRoots, woodRoot;
    public IReadOnlyList<RealmDefinition> Stages => stages;
    public SpiritRoots MixedRoots => mixedRoots;
    public SpiritRoots WoodRoot => woodRoot;

    public static CultivationRealms Table
    {
        get
        {
            if (!table) table = Resources.Load<CultivationRealms>(ResourceName);
            if (!table) throw new InvalidOperationException("缺少首卷境界表 " + ResourceName);
            return table;
        }
    }

    public RealmDefinition Get(RealmMajor major, RealmPhase phase)
    {
        foreach (var stage in stages)
            if (stage.Major == major && stage.Phase == phase) return stage;
        throw new ArgumentOutOfRangeException(nameof(phase), $"本卷未配置境界 {major}/{phase}");
    }

    public RealmDefinition Resolve(bool sensed, int qi, bool founded)
    {
        if (founded) return Get(RealmMajor.Foundation, RealmPhase.Early);
        RealmDefinition resolved = Get(RealmMajor.Mortal, sensed ? RealmPhase.FirstSense : RealmPhase.None);
        foreach (var stage in stages)
            if (stage.Major == RealmMajor.QiRefining && qi >= stage.MinimumQi && stage.MinimumQi > resolved.MinimumQi)
                resolved = stage;
        return resolved;
    }
}
