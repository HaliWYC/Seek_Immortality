using System;
using UnityEngine;

// The numeric order is the five saved pool slots of the original breakthrough.
public enum SpiritElement { Wood = 0, Fire = 1, Earth = 2, Metal = 3, Water = 4 }

[Serializable]
public sealed class SpiritRoots
{
    public static readonly string[] ElementNames = { "木", "火", "土", "金", "水" };
    [SerializeField] string displayName;
    [SerializeField, Range(0, 100)] int wood, fire, earth, metal, water;
    public string Name => displayName;
    public int TotalWeight => wood + fire + earth + metal + water;
    // Trial cultivators have a dominant root and a minor earth root for mixed-element casts.
    public static SpiritRoots FireEarth { get; } = new SpiritRoots { displayName = "火土双灵根", fire = 75, earth = 25 };
    public static SpiritRoots WoodEarth { get; } = new SpiritRoots { displayName = "木土双灵根", wood = 75, earth = 25 };
    public static SpiritRoots For(bool single) => single ? CultivationRealms.Table.WoodRoot : CultivationRealms.Table.MixedRoots;

    public int Weight(SpiritElement element)
    {
        switch (element)
        {
            case SpiritElement.Wood: return wood;
            case SpiritElement.Fire: return fire;
            case SpiritElement.Earth: return earth;
            case SpiritElement.Metal: return metal;
            case SpiritElement.Water: return water;
            default: throw new ArgumentOutOfRangeException(nameof(element));
        }
    }

    // The leading current contributes two pulses, the other pulses follow the cycle.
    // Sample the relative weights deterministically so reloading a turn preserves its intake.
    public SpiritElement IntakeElement(int turn, int pulse)
    {
        if (turn <= 0 || pulse < 0) throw new ArgumentOutOfRangeException();
        int slot = (turn - 1 + Mathf.Max(0, pulse - 1)) % ElementNames.Length;
        int total = TotalWeight;
        if (total <= 0) throw new InvalidOperationException("灵根的五行总权重须大于零。");
        int cursor = (slot * total / ElementNames.Length + (turn - 1) / ElementNames.Length) % total;
        for (int i = 0; i < ElementNames.Length; i++)
        {
            var element = (SpiritElement)i;
            cursor -= Weight(element);
            if (cursor < 0) return element;
        }
        throw new InvalidOperationException("五行灵根权重超限。");
    }
}
