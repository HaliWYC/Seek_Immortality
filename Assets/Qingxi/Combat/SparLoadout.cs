using System;
using System.Collections.Generic;

public sealed class SparLoadout
{
    public const int ComparisonSeed = 0;
    public string Name { get; }
    public SpiritRoots Roots { get; }
    public CultivationManualDefinition Manual { get; }
    public IReadOnlyList<QingxiSpellDefinition> Spells { get; }
    public string Tactic { get; }
    public int GearAttack { get; }
    public int GearHealth { get; }

    readonly CultivatorBag bag;
    readonly CultivationItem relic;
    readonly CultivationItem[] carried;

    SparLoadout(string name, SpiritRoots roots, CultivationManualDefinition manual,
        IReadOnlyList<QingxiSpellDefinition> spells, string tactic, CultivatorBag bag = null,
        int gearAttack = 0, int gearHealth = 0)
    {
        Name = name;
        Roots = roots;
        Manual = manual;
        Spells = spells;
        Tactic = tactic;
        GearAttack = gearAttack;
        GearHealth = gearHealth;
        this.bag = bag;
        if (bag == null) return;
        relic = bag.Equipped(CultivationGearSlot.Accessory);
        carried = new CultivationItem[CultivatorBag.CarrySlotCount];
        for (int slot = 0; slot < carried.Length; slot++) carried[slot] = bag.Carried(slot);
    }

    public static SparLoadout Personal()
    {
        var state = DemoRun.State;
        var gear = state?.Attributes(DemoRun.Sensed);
        return new SparLoadout("本人所学", SpiritRoots.For(DemoRun.SingleRoot),
            state?.MainManual, state?.PreparedSpells ?? Array.Empty<QingxiSpellDefinition>(),
            "使用本人灵根、功法、装备与携带槽。本场用药为演练，重开时恢复。", state?.Bag,
            gear?.EquipmentAttack ?? 0, gear?.EquipmentHealth ?? 0);
    }

    public static SparLoadout[] PracticeOptions() => new[]
    {
        Personal(),
        new SparLoadout("赤炉火法", SpiritRoots.FireEarth, QingxiArts.EmberManual,
            new[] { QingxiArts.Firebolt, QingxiArts.Pulse }, "火灵根 75，土灵根 25。赤炉诀强化火弹的灼烧，留土配合气指。"),
        new SparLoadout("青息缚枝", SpiritRoots.WoodEarth, QingxiArts.SpringManual,
            new[] { QingxiArts.Vine, QingxiArts.Pulse }, "木灵根 75，土灵根 25。进入三格内缚住追兵，再用余下步数拉开距离。"),
        new SparLoadout("归元合气", SpiritRoots.For(false), QingxiArts.Guiyuan,
            new[] { QingxiArts.Pulse, QingxiArts.Firebolt }, "五行权重均等。归元篇额外引气，择时以相生或相克配出合气指。"),
        new SparLoadout("赤炉护体", SpiritRoots.FireEarth, QingxiArts.EmberManual,
            new[] { QingxiArts.Firebolt, QingxiArts.Ward }, "火弹附灼烧。厚土配离火施护体诀，相生后两次各可挡三点。")
    };

    public void ApplyTo(SparMatch match, int seed = ComparisonSeed)
    {
        match.ConfigurePlayerLoadout(Roots, Manual, Spells, seed, relic?.GrantedSpell);
        if (carried != null)
            match.ConfigureCarried(carried, match.InField ? bag.TryConsumeCarried : null);
        if (!match.InField)
            match.ConfigureUnitLoadout(match.Units[3], SpiritRoots.For(true), QingxiArts.SpringManual,
                new[] { QingxiArts.Vine }, seed);
    }

    public string Equipment
    {
        get
        {
            string spells = "";
            foreach (var spell in Spells) spells += (spells.Length == 0 ? "" : "、") + spell.Name;
            if (relic?.GrantedSpell is QingxiSpellDefinition granted)
                spells += (spells.Length == 0 ? "" : "、") + granted.Name + "（法宝）";
            return $"{Roots.Name}  ·  {(Manual != null ? Manual.Name : "未设主修")}  ·  {(spells.Length > 0 ? spells : "常击")}";
        }
    }
}
