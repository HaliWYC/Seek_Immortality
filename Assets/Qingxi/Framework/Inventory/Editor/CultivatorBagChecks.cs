using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CultivatorBagChecks
{
    [MenuItem("行旅/验证角色行囊")]
    public static void Run()
    {
        CheckCatalog();
        CheckTransactions();
        CheckOldSave();
        CheckQuest();
        CheckEquipment();
        CheckBattlePreparation();
        Debug.Log("角色行囊验证通过。");
    }

    static void CheckCatalog()
    {
        var ids = new HashSet<string>();
        foreach (var item in FirstVolumeItems.All)
        {
            Must(!string.IsNullOrWhiteSpace(item.Id) && ids.Add(item.Id), "物品必须有独立的存档标识。");
            Must(item.StackSize > 0 && item.Price >= 0 && !string.IsNullOrEmpty(item.Name), "名称、堆叠与现价有效。");
            if (item.Kind == CultivationItemKind.Equipment)
                Must(item.GearSlot != CultivationGearSlot.None && !item.ReservedPocket && item.StackSize == 1,
                    "装备对应一个穿戴位置，单件占一格。");
        }
        Must(FirstVolumeItems.Find(FirstVolumeItems.SpiritStone).Kind == CultivationItemKind.Currency, "灵石可另存。");
    }

    static void CheckTransactions()
    {
        var bag = new CultivatorBag();
        Must(bag.Capacity == 8 && bag.UsedSlots == 0 && bag.ReservedSlots == 3, "空囊有三种物品的留格。");
        Must(bag.TryAdd(FirstVolumeItems.JadeHerb, 48) && !bag.TryAdd(FirstVolumeItems.JadeHerb, 1), "普通物资不得占用另两类的保留格。");
        Must(bag.TryAdd(FirstVolumeItems.FoundationPill, 6) && bag.TryAdd(FirstVolumeItems.MeridianDew, 1), "丹和露均可收入。");
        Must(bag.UsedSlots == 8 && bag.ReservedSlots == 0, "按实际堆叠占用格数。");
        Must(!bag.TryExchange(FirstVolumeItems.JadeHerb, 1, FirstVolumeItems.FoundationPill, 1) &&
            bag.Count(FirstVolumeItems.JadeHerb) == 48, "换入丹药无格时不扣芝。");
        Must(bag.TryExchange(FirstVolumeItems.JadeHerb, 8, FirstVolumeItems.FoundationPill, 1), "交换时可以使用材料腾出的格子。");
        Must(!bag.TryRemove(FirstVolumeItems.JadeHerb, 41) && !bag.TryAdd("missing", 1), "不足与无定义物品均无变动。");
        Must(bag.TryAdd(FirstVolumeItems.SpiritStone, int.MaxValue) && !bag.TryAdd(FirstVolumeItems.SpiritStone, 1), "灵石不占格，数量不溢出。");
        Must(bag.TryRemove(FirstVolumeItems.FoundationPill, 7) && bag.ReservedSlots == 1, "可以从多叠取尽并腾出格子。");
    }

    static void CheckOldSave()
    {
        const string old = "{\"stones\":11,\"herbs\":120,\"pills\":8,\"dew\":true,\"turn\":3,\"usedPills\":2}";
        var state = JsonUtility.FromJson<DemoCultivation>(old);
        Must(state.Stones == 11 && state.Herbs == 120 && state.Pills == 8 && state.Dew, "旧账原数入囊。");
        Must(state.InTrial && state.Turn == 3 && state.UsedPills == 2 && state.Bag.Capacity == 18, "超出新格数的旧物保留，已用丹不退回。");
        Must(state.Spend(7) && state.Bag.TryRemove(FirstVolumeItems.FoundationPill, 3), "迁移后的物资能正常消耗。");
        var restored = JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(state));
        Must(restored.Stones == 4 && restored.Herbs == 120 && restored.Pills == 5 && restored.Dew, "重载不会再导一次旧账。");
        var untouched = new DemoCultivation();
        Must(untouched.Herbs == 0 && untouched.Stones == 0 && untouched.Bag.Capacity == 8, "另一角色另有空囊。");
    }

    static void CheckQuest()
    {
        var state = new DemoCultivation();
        state.AcceptManual();
        state.TakePact();
        state.Earn(20);
        Must(state.Bag.TryAdd(FirstVolumeItems.JadeHerb, 40) &&
            state.Bag.TryAdd(FirstVolumeItems.FoundationPill, 12), "构造七格实物加一格留空的行囊。");
        Must(!state.Gather() && !state.BuyPill() && !state.MakePill() && !state.DeliverPact(), "满囊操作可以重试。");
        Must(state.Herbs == 40 && state.Pills == 12 && state.Stones == 20 && state.Days == 0 && !state.Fulfilled, "失败不扣货、不消耗约期。");
        state.Bag.TryRemove(FirstVolumeItems.JadeHerb, 8);
        Must(state.Gather() && state.Herbs == 34, "整理后仍能领取头茬两株。");
        state.Bag.TryRemove(FirstVolumeItems.FoundationPill, 1);
        Must(state.DeliverPact() && state.Herbs == 33 && state.Pills == 12 && state.Stones == 22 && !state.DeliverPact(), "落袋后才结清唯一报偿。");
    }

    static void CheckEquipment()
    {
        var state = JsonUtility.FromJson<DemoCultivation>("{\"qi\":9,\"stones\":11,\"herbs\":40}");
        var bag = state.Bag;
        var baseAttributes = state.Attributes(true);
        Must(!bag.StartingGearClaimed && !bag.TryClaimStartingGear() && bag.Count(FirstVolumeItems.TravelBlade) == 0 &&
            bag.Count(FirstVolumeItems.TravelCoat) == 0 && state.Herbs == 40 && state.Stones == 11,
            "旧行囊只有一格时不能领半套，也不改旧物数量。");
        Must(bag.TryRemove(FirstVolumeItems.JadeHerb, 8) && bag.TryClaimStartingGear() && bag.StartingGearClaimed &&
            !bag.TryClaimStartingGear() && bag.UsedSlots == 6 && bag.ReservedSlots == 2, "整理后可领取一次，两件各占一格。");
        Must(!bag.TryEquip(FirstVolumeItems.JadeHerb) && bag.TryEquip(FirstVolumeItems.TravelBlade) &&
            !bag.TryEquip(FirstVolumeItems.TravelBlade) && bag.TryEquip(FirstVolumeItems.TravelCoat) && bag.UsedSlots == 4,
            "装备从行囊移入对应位置，不能穿戴草药或重复计入同一件行装。");
        var dressed = state.Attributes(true);
        Must(dressed.Attack == baseAttributes.Attack + 1 && dressed.MaxHealth == baseAttributes.MaxHealth + 3 &&
            dressed.ManaCapacity == baseAttributes.ManaCapacity, "穿戴后重新汇总属性，术威与气血上限实际生效。");

        var deployment = new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.one };
        var field = new SparMatch(new bool[3, 3], deployment, party: FieldParty.Solo, heroAttributes: dressed);
        var practice = new SparMatch(new bool[3, 3], deployment, party: FieldParty.Practice, heroAttributes: dressed);
        Must(field.Units[0].Power == dressed.Attack && field.Units[0].MaxHp == dressed.MaxHealth &&
            practice.Units[0].Power == 4 && practice.Units[0].MaxHp == 12, "装备影响野外开战，切磋仍用统一数值。");

        var restored = JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(state));
        bag = restored.Bag;
        Must(bag.StartingGearClaimed && bag.Equipped(CultivationGearSlot.Weapon)?.Id == FirstVolumeItems.TravelBlade &&
            bag.Equipped(CultivationGearSlot.Clothing)?.Id == FirstVolumeItems.TravelCoat &&
            restored.Attributes(true).Attack == dressed.Attack && restored.Attributes(true).MaxHealth == dressed.MaxHealth,
            "存读档恢复穿戴和领取标记，装备加成只结算一次。");
        Must(bag.TryAdd(FirstVolumeItems.JadeHerb, 16) && !bag.TryUnequip(CultivationGearSlot.Clothing) &&
            bag.Count(FirstVolumeItems.TravelCoat) == 0 && restored.Attributes(true).MaxHealth == dressed.MaxHealth,
            "满囊时卸下失败，穿戴、属性和物品数量均保持正确。");
        Must(bag.TryRemove(FirstVolumeItems.JadeHerb, 8) && bag.TryUnequip(CultivationGearSlot.Clothing) &&
            bag.Count(FirstVolumeItems.TravelCoat) == 1 && bag.Equipped(CultivationGearSlot.Clothing) == null &&
            restored.Attributes(true).MaxHealth == baseAttributes.MaxHealth, "腾出一格后可卸回行囊，气血加成同步撤回。");
    }

    static void CheckBattlePreparation()
    {
        var pendant = FirstVolumeItems.Find(FirstVolumeItems.SpiritPendant);
        var pill = FirstVolumeItems.Find(FirstVolumeItems.SpringPill);
        var seal = FirstVolumeItems.Find(FirstVolumeItems.RoadSeal);
        var ray = QingxiArts.JadeRay;
        Must(pendant.GearSlot == CultivationGearSlot.Accessory && pendant.AttackBonus == 1 &&
            pendant.GrantedSpell == ray && ray.IsBattleSpell && ray.FaShuType == FaShuType.法宝 &&
            ray.SourceType == FaShuSourceType.携带物品 && ray.ManaCost == 3 && ray.SameCost == 1 &&
            ray.Range == 4 && ray.ExtraPower == 1 && ray.CooldownTurns == 1, "灵纹玉佩按法宝的定义授予玉芒。");
        Must(pill.CanCarry && pill.CarryEffect == CultivationCarryEffect.Heal && pill.CarryHeal == 4 &&
            pill.CarryActionCost == 2 && pill.StackSize == 6, "回春丹有独立的战斗回复规则。");
        Must(seal.CanCarry && seal.CarryEffect == CultivationCarryEffect.BreakFormation &&
            seal.CarryActionCost == 2 && seal.SlotsFor(1) == 0, "破阵符保留委托物收纳并可备入携带槽。");

        var state = JsonUtility.FromJson<DemoCultivation>("{\"qi\":9,\"stones\":11,\"herbs\":40}");
        var bag = state.Bag;
        int baseAttack = state.Attributes(true).Attack;
        Must(bag.Carried(0) == null && bag.Carried(1) == null && !bag.BattleKitClaimed &&
            !bag.TryClaimBattleKit() && bag.Count(pendant.Id) == 0 && bag.Count(pill.Id) == 0 && state.Stones == 11,
            "旧档两个携带槽为空，满囊时不能领取部分物资。");
        Must(bag.TryRemove(FirstVolumeItems.JadeHerb, 8) && bag.TryClaimBattleKit() && !bag.TryClaimBattleKit() &&
            bag.Count(pendant.Id) == 1 && bag.Count(pill.Id) == 2 && bag.UsedSlots == 6, "试斗物资只领取一次。");
        Must(bag.TryEquip(pendant.Id) && bag.Equipped(CultivationGearSlot.Accessory) == pendant &&
            state.Attributes(true).Attack == baseAttack + 1 && bag.UsedSlots == 5, "佩戴玉佩可增加实际术威。");
        Must(state.AcceptManual() && !state.CanStudyAtGate(ray, true) &&
            !state.StudyAtGate(ray, true) && !state.TogglePrepared(ray), "玉芒只随装备授予，不可通过门派备法习得。");
        Must(!bag.TryPack(-1, pill.Id) && !bag.TryPack(CultivatorBag.CarrySlotCount, pill.Id) &&
            !bag.TryPack(0, FirstVolumeItems.JadeHerb) &&
            bag.TryPack(0, pill.Id) && bag.Count(pill.Id) == 1 && bag.UsedSlots == 5 &&
            !bag.TryPack(0, pill.Id) && bag.TryPack(1, pill.Id) && bag.Count(pill.Id) == 0 && bag.UsedSlots == 4,
            "两槽各存一件，最后一枚丹药备入时腾出堆叠格。");

        var restored = JsonUtility.FromJson<DemoCultivation>(JsonUtility.ToJson(state));
        bag = restored.Bag;
        Must(bag.BattleKitClaimed && bag.Equipped(CultivationGearSlot.Accessory) == pendant &&
            bag.Carried(0) == pill && bag.Carried(1) == pill && restored.Attributes(true).Attack == baseAttack + 1 &&
            bag.Count(pill.Id) == 0, "读档保留法宝、携带槽和领取标记且不重复奖励。");

        Must(bag.TryAdd(FirstVolumeItems.JadeHerb, 16) && bag.TryAdd(seal.Id, 1) &&
            !bag.TryUnpack(0) && !bag.TryPack(0, seal.Id) &&
            !bag.TryConsumeCarried(0, seal.Id) && bag.Carried(0) == pill && bag.Count(seal.Id) == 1,
            "行囊已满时取回和交换均保留原物，结算只能消耗匹配的槽位。");
        Must(bag.TryRemove(FirstVolumeItems.JadeHerb, 8) && bag.TryPack(0, seal.Id) &&
            bag.Carried(0) == seal && bag.Count(pill.Id) == 1 && bag.Count(seal.Id) == 0 &&
            bag.TryUnpack(0) && bag.Count(seal.Id) == 1, "整理后可调换携带物，独立存放的破阵符可取回。");
        Must(bag.TryConsumeCarried(1, pill.Id) && !bag.TryConsumeCarried(1, pill.Id) &&
            bag.Carried(1) == null && bag.Count(pill.Id) == 1, "消耗随身丹药只扣指定槽，不扣行囊余量。");
    }

    static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
