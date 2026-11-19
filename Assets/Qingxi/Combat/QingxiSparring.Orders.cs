using System.Collections.Generic;
using UnityEngine;

public sealed partial class QingxiSparring
{
    enum OrderKind { None, Move, Attack, Bandage, Carried }

    OrderKind pending;
    SparUnit orderActor;
    Vector2Int orderCell;
    int orderCarrySlot = -1;
    int orderInputFrame = -1;
    public bool HasOrder => pending != OrderKind.None;
    public bool OrderInputThisFrame => orderInputFrame == Time.frameCount;
    public string OrderDescription { get; private set; }
    public IReadOnlyList<Vector2Int> OrderPath { get; private set; }
    public Vector2Int? OrderTarget => HasOrder ? orderCell : (Vector2Int?)null;
    public int PendingCarriedSlot => pending == OrderKind.Carried ? orderCarrySlot : -1;

    // Changing the active unit, withdrawing, or closing the board discards the preview.
    public void CancelOrder()
    {
        pending = OrderKind.None;
        orderActor = null;
        orderCarrySlot = -1;
        OrderDescription = "";
        OrderPath = null;
    }

    public void CancelOrYield()
    {
        if (JournalOpen || orderInputFrame == Time.frameCount) return;
        if (!HasOrder) { ToggleYield(); return; }
        orderInputFrame = Time.frameCount;
        CancelOrder();
        feedback = "已撤销。";
        Refresh();
    }

    public void ConfirmOrEndTurn()
    {
        if (!AwaitingPlayer || orderInputFrame == Time.frameCount) return;
        if (HasOrder) ConfirmOrder();
        else
        {
            orderInputFrame = Time.frameCount;
            FinishTurn();
        }
    }

    void Preview(OrderKind kind, Vector2Int cell, string description, IReadOnlyList<Vector2Int> path = null, int carrySlot = -1)
    {
        if (orderInputFrame == Time.frameCount) return;
        if (pending == kind && orderCell == cell && orderActor == selected && orderCarrySlot == carrySlot)
        { ConfirmOrder(); return; }
        orderInputFrame = Time.frameCount;
        pending = kind;
        orderActor = selected;
        orderCell = cell;
        orderCarrySlot = carrySlot;
        OrderPath = path;
        OrderDescription = description;
        feedback = "";
        Refresh();
    }

    void ConfirmOrder()
    {
        if (orderInputFrame == Time.frameCount || !AwaitingPlayer || !HasOrder) return;
        orderInputFrame = Time.frameCount;
        var kind = pending;
        var actor = orderActor;
        var cell = orderCell;
        int carrySlot = orderCarrySlot;
        CancelOrder();
        bool applied = false;
        if (actor == selected && Match.CanCommand(actor) && !confirmingYield)
        {
            switch (kind)
            {
                case OrderKind.Move: applied = Match.Move(actor, cell); break;
                case OrderKind.Attack:
                case OrderKind.Bandage:
                case OrderKind.Carried: applied = QueuePlayerAttack(kind, actor, cell, carrySlot); break;
            }
        }
        if (!applied) feedback = "局势已变，请重新选定目标。";
        else if (kind == OrderKind.Move)
        {
            FollowBattleActor();
            feedback = "";
        }
        Refresh();
    }

    public void UseFieldHerb()
    {
        if (!AwaitingPlayer || orderInputFrame == Time.frameCount) return;
        if (Match.Party == FieldParty.Brigands)
        {
            int slot = Match.FindCarriedSlot(FirstVolumeItems.RoadSeal);
            if (slot >= 0) { SelectCarriedSlot(slot); return; }
            CancelOrder();
            feedback = "破阵符须在开战前装入携带槽。";
            Refresh();
            return;
        }
        if (Match.CanUseAgreedHerb(selected))
        {
            int heal = Mathf.Min(4, selected.MaxHp - selected.Hp);
            Preview(OrderKind.Bandage, selected.Cell, $"止血散 → {selected.Name}　回复 {heal} 气血，消耗 1 份药　·　{ActionPointPreview(SparMatch.ItemCost)}");
        }
        else
        {
            CancelOrder();
            feedback = selected.ActionPoints < SparMatch.ItemCost ? $"使用物品需要 {SparMatch.ItemCost} 点行动力。" :
                "约定的止血散只可由受伤的阿荇使用。";
            Refresh();
        }
    }

    public void SelectCarriedSlot(int slot)
    {
        if (!AwaitingPlayer || !Match.HasCarriedSlots || orderInputFrame == Time.frameCount) return;
        string reason = Match.CarriedUnavailableReason(selected, slot);
        if (reason.Length != 0)
        {
            CancelOrder();
            feedback = reason + "。";
            Refresh();
            return;
        }

        var item = Match.Carried(slot);
        var target = Match.CarriedTarget(slot);
        string result = item.CarryEffect == CultivationCarryEffect.Heal
            ? $"预计回复 {Mathf.Min(item.CarryHeal, selected.MaxHp - selected.Hp)} 点气血"
            : Match.WardStudied ? "破去护阵并阻敌一次行动" : "击伤灰衣客";
        string payment = Match.InField ? "消耗 1 件" : "本场演练消耗 1 件";
        Preview(OrderKind.Carried, target.Cell,
            $"{item.Name} → {target.Name}　{result}　·　{payment}　·　{ActionPointPreview(item.CarryActionCost)}", carrySlot: slot);
    }

    void PreviewCell(Vector2Int cell, SparUnit target)
    {
        if (target != null && Match.CanAttack(selected, cell))
        {
            bool useSpell = Match.SpellSelected && selected == Match.Units[0];
            string action = useSpell
                ? (Match.IsRelicSpell(Match.PlayerSpell) ? "法宝 · " : "") + Match.PlayerSpell.Name : "常击";
            string resources = useSpell ? SpellPaymentAndEffect() : ActionPointPreview(SparMatch.StrikeCost);
            string result = "";
            if (target != selected)
            {
                int blocked = Match.PreviewAttackBlock(selected, cell);
                result = $"　预计损 {Match.PreviewAttackDamage(selected, cell)} 气血";
                if (blocked > 0) result += $"，护体挡 {blocked}";
            }
            Preview(OrderKind.Attack, cell, $"{action} → {target.Name}{result}　·　{resources}");
            return;
        }
        if (target == null)
        {
            var path = Match.PreviewPath(selected, cell);
            if (path.Count > 0)
            {
                int cost = path.Count - 1;
                Preview(OrderKind.Move, cell, $"移至（{cell.x + 1}，{cell.y + 1}）　沿标线走 {cost} 格　·　{ActionPointPreview(cost)}", path);
                return;
            }
        }
        CancelOrder();
        if (target != null)
        {
            string reason = Match.SpellSelected ? Match.SpellUnavailableReason(selected, Match.PlayerSpell) :
                selected.ActionPoints < SparMatch.StrikeCost ? $"常击需要 {SparMatch.StrikeCost} 点行动力" : "";
            if (reason.Length > 0) feedback = reason + "。";
            else if (Match.SpellSelected && Match.PlayerSpell.Target == FaShuTarget.Self) feedback = "此术以自己为目标。";
            else feedback = !Match.HasLineOfSight(selected.Cell, cell) ?
                "草木山石遮住了视线，换个落脚处再出招。" : "对手在射程之外。";
        }
        else
            feedback = Match.IsBlocked(cell) ? "此处地形无法落脚。" : selected.ActionPoints == 0 ?
                "本回合行动力已用尽。" : selected.MovementLeft == 0 ? "缚枝限制了本回合的移动。" :
                $"最多还能走 {selected.MovementLeft} 格，请选青色范围内的空格。";
        Refresh();
    }

    string ActionPointPreview(int cost) => $"行动力 {selected.ActionPoints} → {selected.ActionPoints - cost}";
}
