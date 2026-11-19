using System;
using UnityEngine;

[Serializable]
public sealed class BrigandTerms
{
    public const int Toll = 2;
    public const int WardPrice = 3;
    public const int Bounty = 7;

    public bool studied, wardReady, wardSpent, paid, fighting;
    public string result;
    public bool Settled => !string.IsNullOrEmpty(result);
    public bool WardPacked
    {
        get
        {
            if (!DemoRun.Active || !wardReady || wardSpent || Settled) return false;
            for (int slot = 0; slot < CultivatorBag.CarrySlotCount; slot++)
                if (DemoRun.State.Bag.Carried(slot)?.Id == FirstVolumeItems.RoadSeal) return true;
            return false;
        }
    }

    public static BrigandTerms Load()
    {
        string raw = DemoRun.AmbushData;
        if (string.IsNullOrEmpty(raw)) return new BrigandTerms();
        try { return JsonUtility.FromJson<BrigandTerms>(raw) ?? new BrigandTerms(); }
        catch (ArgumentException) { return new BrigandTerms(); }
    }

    public void Save()
    {
        DemoRun.AmbushData = JsonUtility.ToJson(this);
        DemoRun.Save();
    }

    // Stable for the life of this character, including after a return to the pass.
    public static bool CutsOffRoad => CutsOffRoadFor(DemoRun.AmbushSeed);
    public static bool CutsOffRoadFor(string id)
    {
        uint hash = 2166136261u;
        foreach (char c in id) hash = unchecked((hash ^ c) * 16777619u);
        return (hash & 1u) != 0;
    }

    public bool Examine()
    {
        if (studied || fighting || Settled) return false;
        studied = true;
        DemoRun.State.AddTravelMinutes(60);
        Save();
        return true;
    }

    public bool PrepareWard()
    {
        if (fighting || Settled || wardReady || wardSpent || !DemoRun.Active || DemoRun.Slain ||
            !DemoRun.State.Bag.TryExchange(FirstVolumeItems.SpiritStone, WardPrice, FirstVolumeItems.RoadSeal, 1)) return false;
        wardReady = true;
        Save();
        return true;
    }

    public static void RestoreLegacyWard()
    {
        if (!DemoRun.Active || DemoRun.Slain) return;
        var state = Load();
        var bag = DemoRun.State.Bag;
        if (state.wardReady && !state.wardSpent && !state.Settled && !state.fighting &&
            !state.WardPacked && bag.Count(FirstVolumeItems.RoadSeal) == 0)
            bag.TryAdd(FirstVolumeItems.RoadSeal, 1);
    }

    public bool PayToll()
    {
        if (Settled || paid || fighting || !DemoRun.SpendStones(Toll)) return false;
        paid = true;
        if (!CutsOffRoad) result = "灰衣客收下两枚灵石，让出了归路。你与他相安而过。";
        Save();
        return true;
    }

    public void Escape()
    {
        if (!fighting && !paid || Settled) return;
        int loss = fighting ? Math.Min(Toll, DemoRun.State.Stones) : 0;
        DemoRun.SpendStones(loss);
        DemoRun.State.AddTravelMinutes(1440d);
        result = fighting ? $"你引开同伙，绕行一日脱身，丢下了 {loss} 枚灵石。" :
            "同伙收了钱仍要封路。你趁他伸手时折返西坡，绕行一日脱身。";
        fighting = false;
        Save();
    }

    public static void ResolveInterruptedDuel()
    {
        if (DemoRun.Active && !DemoRun.Slain)
        {
            var state = Load();
            if (state.fighting) state.Escape();
        }
    }
}
