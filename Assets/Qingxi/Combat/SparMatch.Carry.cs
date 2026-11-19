using System;
using System.Collections.Generic;

public sealed partial class SparMatch
{
    readonly CultivationItem[] carriedItems = new CultivationItem[CultivatorBag.CarrySlotCount];
    Func<int, string, bool> consumeCarried;

    public bool HasCarriedSlots { get; private set; }

    public void ConfigureCarried(IReadOnlyList<CultivationItem> carried, Func<int, string, bool> consume = null)
    {
        if (HasCarriedSlots || TurnNumber != 1 || ActiveUnit.Moved || ActiveUnit.Acted)
            throw new InvalidOperationException("携带槽须在开战前确定。");
        if (carried == null || carried.Count != carriedItems.Length)
            throw new ArgumentException("携带槽数量不符。", nameof(carried));
        for (int slot = 0; slot < carriedItems.Length; slot++)
            if (carried[slot]?.CanCarry == true) carriedItems[slot] = carried[slot];
        consumeCarried = consume;
        HasCarriedSlots = true;
    }

    public CultivationItem Carried(int slot) =>
        slot >= 0 && slot < carriedItems.Length ? carriedItems[slot] : null;

    public int FindCarriedSlot(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int slot = 0; slot < carriedItems.Length; slot++)
            if (carriedItems[slot]?.Id == id) return slot;
        return -1;
    }

    internal SparUnit CarriedTarget(int slot) => Carried(slot)?.CarryEffect switch
    {
        CultivationCarryEffect.Heal => units[0],
        CultivationCarryEffect.BreakFormation when Party == FieldParty.Brigands => units[2],
        _ => null
    };

    public bool CanUseCarried(SparUnit unit, int slot) => CarriedUnavailableReason(unit, slot).Length == 0;

    public string CarriedUnavailableReason(SparUnit unit, int slot)
    {
        if (unit != units[0] || !CanCommand(unit)) return "须轮到本人行动";
        var item = Carried(slot);
        if (item == null) return "携带槽为空，需在开战前从行囊装入";
        if (item.CarryEffect == CultivationCarryEffect.BreakFormation)
        {
            if (Party != FieldParty.Brigands) return "此符只可对灰衣客的护阵使用";
            if (!WardReady || !units[2].Alive) return "当前已无可破的护阵";
            if (!CanUseFieldWard(unit)) return $"需要 {ItemCost} 点行动力";
        }
        else if (unit.Hp == unit.MaxHp) return "气血已满";
        if (unit.ActionPoints < item.CarryActionCost) return $"需要 {item.CarryActionCost} 点行动力";
        return "";
    }

    public bool UseCarried(SparUnit unit, int slot)
    {
        if (!CanUseCarried(unit, slot)) return false;
        var item = carriedItems[slot];
        if (InField && consumeCarried != null && !consumeCarried(slot, item.Id)) return false;

        if (item.CarryEffect == CultivationCarryEffect.BreakFormation) ApplyFieldWard(unit);
        else
        {
            ClearActionResults();
            int healed = Heal(unit, item.CarryHeal);
            unit.ActionPoints -= item.CarryActionCost;
            unit.Acted = true;
            Message = $"{unit.Name}服下{item.Name}，恢复 {healed} 点气血。";
            RecordJournalAction(item.Name, unit);
        }
        carriedItems[slot] = null;
        return true;
    }
}
