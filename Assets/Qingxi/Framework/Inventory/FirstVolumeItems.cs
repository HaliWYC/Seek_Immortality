using System;
using System.Collections.Generic;
using UnityEngine;

public enum CultivationItemKind { Currency, Herb, Pill, Medicine, Equipment, Quest }
public enum CultivationGearSlot { None, Weapon, Clothing, Accessory }
public enum CultivationCarryEffect { None, Heal, BreakFormation }

[Serializable]
public sealed class CultivationItem
{
    [SerializeField] string id, displayName;
    [SerializeField] CultivationItemKind kind;
    [SerializeField, Min(1)] int stackSize = 1;
    [SerializeField, Min(0)] int price;
    [SerializeField] bool reservedPocket;
    [SerializeField, TextArea] string description;
    [SerializeField] CultivationGearSlot gearSlot;
    [SerializeField, Min(0)] int attackBonus, healthBonus;
    [SerializeField] string grantedSpellId;
    [SerializeField] CultivationCarryEffect carryEffect;
    [SerializeField, Min(0)] int carryHeal, carryActionCost;

    public string Id => id;
    public string Name => displayName;
    public CultivationItemKind Kind => kind;
    public int StackSize => stackSize;
    public int Price => price;
    public bool ReservedPocket => reservedPocket;
    public string Description => description;
    public CultivationGearSlot GearSlot => gearSlot;
    public int AttackBonus => attackBonus;
    public int HealthBonus => healthBonus;
    public QingxiSpellDefinition GrantedSpell => string.IsNullOrEmpty(grantedSpellId) ? null :
        QingxiArts.Find(grantedSpellId) as QingxiSpellDefinition;
    public CultivationCarryEffect CarryEffect => carryEffect;
    public bool CanCarry => carryEffect == CultivationCarryEffect.BreakFormation ||
        (carryEffect == CultivationCarryEffect.Heal && carryHeal > 0);
    public int CarryHeal => carryHeal;
    public int CarryActionCost => carryActionCost;

    public string KindName => kind switch
    {
        CultivationItemKind.Currency => "通货",
        CultivationItemKind.Herb => "草药",
        CultivationItemKind.Pill => "丹药",
        CultivationItemKind.Medicine => "药露",
        CultivationItemKind.Equipment => GearSlotName,
        CultivationItemKind.Quest => "委托物件",
        _ => "物件"
    };

    public string GearSlotName => gearSlot switch
    {
        CultivationGearSlot.Weapon => "武器",
        CultivationGearSlot.Clothing => "防具",
        CultivationGearSlot.Accessory => "饰品",
        _ => "装备"
    };

    public int SlotsFor(int count) => kind == CultivationItemKind.Currency || kind == CultivationItemKind.Quest || count == 0
        ? 0 : 1 + (count - 1) / stackSize;

    internal int SlotsWithReservation(int count) => Math.Max(reservedPocket ? 1 : 0, SlotsFor(count));
}

[CreateAssetMenu(menuName = "青岚山境/物品目录")]
public sealed class FirstVolumeItems : ScriptableObject
{
    public const string SpiritStone = "currency.spirit_stone";
    public const string JadeHerb = "herb.jade_marrow";
    public const string FoundationPill = "pill.foundation";
    public const string MeridianDew = "medicine.meridian_dew";
    public const string TravelBlade = "gear.qingxi_blade";
    public const string TravelCoat = "gear.travel_coat";
    public const string SpiritPendant = "gear.spirit_pendant";
    public const string SpringPill = "pill.spring";
    public const string RoadSeal = "quest.road_ward";

    [SerializeField] CultivationItem[] items;
    static FirstVolumeItems catalog;
    static FirstVolumeItems Catalog
    {
        get
        {
            if (!catalog) catalog = Resources.Load<FirstVolumeItems>("QinglanItems");
            if (!catalog) throw new InvalidOperationException("找不到物品目录 QinglanItems。");
            return catalog;
        }
    }

    public static IReadOnlyList<CultivationItem> All => Catalog.items;

    public static CultivationItem Find(string id)
    {
        foreach (var item in Catalog.items)
            if (item.Id == id) return item;
        return null;
    }
}
