using System.Collections.Generic;
using UnityEngine;

public sealed partial class DemoRun
{
    enum BagView { Physical, Gear, Remedies, Commissions }

    readonly struct BagPile
    {
        public readonly string Id, Name;
        public readonly int Count, PerStack;

        public BagPile(string id, string name, int count, int perStack)
        {
            Id = id;
            Name = name;
            Count = count;
            PerStack = perStack;
        }

        public int Stacks => 1 + (Count - 1) / PerStack;
        public int At(int stack) => Mathf.Min(PerStack, Count - stack * PerStack);
    }

    static readonly Color BagPanel = new Color32(35, 55, 47, 255);
    static readonly Color BagInset = new Color32(26, 45, 38, 255);
    static readonly Color BagEdge = new Color32(77, 103, 86, 255);
    static readonly Color BagQuiet = new Color32(170, 190, 172, 255);
    static readonly Color BagJade = new Color32(157, 211, 174, 255);
    static readonly Color BagClay = new Color32(233, 178, 140, 255);
    static readonly Color BagWater = new Color32(161, 209, 216, 255);
    static readonly string[] BagFilters = { "全部实物", "装备", "草药丹药", "委托物件" };
    static readonly Rect BagViewport = new Rect(94, 302, 568, 174);
    const int BagColumns = 4;
    const int BagRowHeight = 84;
    readonly List<BagPile> bagPiles = new List<BagPile>();
    BagView bagView;
    Vector2 bagScroll;
    CultivationGearSlot bagFocusSlot;
    bool bagFocusEquipped;
    int bagCarryFocus = -1;
    int selectedStack;
    string bagNotice;

    void ResetBagPanel()
    {
        bagView = BagView.Physical;
        bagScroll = Vector2.zero;
        bagFocusSlot = CultivationGearSlot.None;
        bagFocusEquipped = false;
        bagCarryFocus = -1;
        selectedItem = null;
        selectedStack = 0;
        confirmDiscard = false;
        bagNotice = null;
    }

    void FocusGearSlot(CultivationGearSlot slot)
    {
        ResetBagPanel();
        bagView = BagView.Gear;
        bagFocusSlot = slot;
        var bag = State.Bag;
        var equipped = bag.Equipped(slot);
        if (equipped != null)
        {
            bagFocusEquipped = true;
            selectedItem = equipped.Id;
            return;
        }
        foreach (var item in FirstVolumeItems.All)
        {
            if (item.GearSlot != slot || bag.Count(item.Id) == 0) continue;
            selectedItem = item.Id;
            break;
        }
    }

    void DrawBag()
    {
        var bag = State.Bag;
        Block(new Rect(75, 211, 599, 416), BagPanel);
        Block(new Rect(694, 211, 331, 416), BagPanel);
        Block(new Rect(694, 211, 331, 2), BagEdge);
        DrawBagWallet(bag);
        for (int i = 0; i < BagFilters.Length; i++)
        {
            var filter = (BagView)i;
            if (!Action(94 + 138 * i, 258, 128, 34, BagFilters[i], false, bagView == filter) || bagView == filter) continue;
            ResetBagPanel();
            bagView = filter;
        }
        CollectBagPiles(bag);
        ValidateBagSelection();
        DrawBagGrid(bag);
        DrawBagGearSlots(bag);
        DrawBagCarrySlots(bag);
        DrawBagDetail(bag);
    }

    void DrawBagWallet(CultivatorBag bag)
    {
        Words(94, 221, 86, 27, "储物袋", 21, Paper, true);
        Block(new Rect(196, 225, 1, 22), BagEdge);
        Words(210, 223, 156, 27, $"灵石  {State.Stones}", 17, BagJade, true);
        Words(376, 223, 145, 27, $"铜钱  {characterJournal.PurseCoins} 文", 17, Gold, true);
        BagCentered(new Rect(538, 219, 114, 32), $"{bag.UsedSlots} / {bag.Capacity} 格", 17, Paper, true);
    }

    void CollectBagPiles(CultivatorBag bag)
    {
        bagPiles.Clear();
        if (bagView == BagView.Commissions)
        {
            if (characterJournal.Harvested && !characterJournal.Delivered)
                bagPiles.Add(new BagPile(FreshGrass, "鲜青纹草", 1, 1));
            var seal = FirstVolumeItems.Find(FirstVolumeItems.RoadSeal);
            int count = bag.Count(FirstVolumeItems.RoadSeal);
            if (seal != null && count > 0) bagPiles.Add(new BagPile(seal.Id, seal.Name, count, seal.StackSize));
            return;
        }

        foreach (var item in FirstVolumeItems.All)
        {
            if (item.Kind == CultivationItemKind.Currency || item.Id == FirstVolumeItems.RoadSeal) continue;
            bool gear = item.GearSlot != CultivationGearSlot.None;
            if ((bagView == BagView.Gear && !gear) || (bagView == BagView.Remedies && gear)) continue;
            int count = bag.Count(item.Id);
            if (count > 0) bagPiles.Add(new BagPile(item.Id, item.Name, count, item.StackSize));
        }
    }

    void ValidateBagSelection()
    {
        if (bagCarryFocus >= 0)
        {
            selectedItem = State.Bag.Carried(bagCarryFocus)?.Id;
            return;
        }
        if (bagFocusEquipped && State.Bag.Equipped(bagFocusSlot) != null) return;
        bagFocusEquipped = false;
        foreach (var pile in bagPiles)
        {
            if (pile.Id != selectedItem) continue;
            selectedStack = Mathf.Clamp(selectedStack, 0, pile.Stacks - 1);
            return;
        }
        selectedItem = bagFocusSlot == CultivationGearSlot.None && bagPiles.Count > 0 ? bagPiles[0].Id : null;
        selectedStack = 0;
        confirmDiscard = false;
    }

    void DrawBagGrid(CultivatorBag bag)
    {
        Block(new Rect(94, 301, 558, 175), BagInset);
        int activeSlots = 0;
        foreach (var pile in bagPiles) activeSlots += pile.Stacks;
        int used = bag.UsedSlots, reserved = bag.ReservedSlots;
        int slots = bagView == BagView.Physical ? Mathf.Max(used, bag.Capacity) : activeSlots;
        if (slots == 0)
        {
            string message = bagView == BagView.Commissions ? "暂无委托物件" : bagView == BagView.Gear ? "袋中暂无装备" : "暂无草药或丹药";
            BagCentered(new Rect(96, 344, 554, 62), message, 19, BagQuiet);
            return;
        }

        int rows = 1 + (slots - 1) / BagColumns;
        var contents = new Rect(0, 0, 550, Mathf.Max(170, (float)rows * BagRowHeight - 4));
        bagScroll = GUI.BeginScrollView(BagViewport, bagScroll, contents);
        int firstRow = Mathf.Clamp(Mathf.FloorToInt(bagScroll.y / BagRowHeight), 0, rows - 1);
        int lastRow = Mathf.Min(rows - 1, firstRow + Mathf.CeilToInt(BagViewport.height / BagRowHeight));
        for (int row = firstRow; row <= lastRow; row++)
        {
            int rowStart = row * BagColumns;
            for (int column = 0; column < BagColumns && column < slots - rowStart; column++)
            {
                var cell = new Rect(140 * column, BagRowHeight * row, 130, 78);
                int index = rowStart + column;
                if (index < activeSlots) DrawBagPileAt(cell, index);
                else DrawEmptyBagCell(cell, index < used ? "旧物" : index - used < reserved ? "预留" : "空格");
            }
        }
        GUI.EndScrollView();
    }

    void DrawBagPileAt(Rect area, int index)
    {
        foreach (var pile in bagPiles)
        {
            if (index < pile.Stacks)
            {
                DrawBagPile(area, pile, index);
                return;
            }
            index -= pile.Stacks;
        }
    }

    void DrawBagPile(Rect area, BagPile pile, int stack)
    {
        bool chosen = !bagFocusEquipped && bagCarryFocus < 0 && selectedItem == pile.Id && selectedStack == stack;
        bool hover = area.Contains(Event.current.mousePosition);
        Block(area, chosen ? Gold : hover ? BagJade : BagEdge);
        Block(new Rect(area.x + 1, area.y + 1, area.width - 2, area.height - 2), chosen || hover ? Pine : BagPanel);
        DrawBagBadge(new Rect(area.x + 45, area.y + 7, 40, 34), pile.Id);
        BagCentered(new Rect(area.x + 3, area.y + 41, 124, 24), pile.Name, 16, Paper, chosen);
        Words(area.x + 87, area.y + 61, 39, 15, $"×{pile.At(stack)}", 13, BagQuiet, true);
        if (!GUI.Button(area, GUIContent.none, GUIStyle.none)) return;
        selectedItem = pile.Id;
        selectedStack = stack;
        bagFocusSlot = CultivationGearSlot.None;
        bagFocusEquipped = false;
        bagCarryFocus = -1;
        confirmDiscard = false;
        bagNotice = null;
    }

    void DrawEmptyBagCell(Rect area, string title)
    {
        Block(area, BagEdge);
        Block(new Rect(area.x + 1, area.y + 1, area.width - 2, area.height - 2), BagInset);
        BagCentered(area, title, 15, BagQuiet);
    }

    void DrawBagGearSlots(CultivatorBag bag)
    {
        DrawBagGearSlot(bag, CultivationGearSlot.Weapon, new Rect(94, 487, 180, 42));
        DrawBagGearSlot(bag, CultivationGearSlot.Clothing, new Rect(283, 487, 180, 42));
        DrawBagGearSlot(bag, CultivationGearSlot.Accessory, new Rect(472, 487, 180, 42));
    }

    void DrawBagGearSlot(CultivatorBag bag, CultivationGearSlot slot, Rect area)
    {
        var item = bag.Equipped(slot);
        bool focused = bagFocusSlot == slot;
        Block(area, focused ? Gold : BagEdge);
        Block(new Rect(area.x + 1, area.y + 1, area.width - 2, area.height - 2), focused || area.Contains(Event.current.mousePosition) ? Pine : BagInset);
        Words(area.x + 10, area.y, 48, area.height, BagSlotName(slot), 15, Gold, true);
        Words(area.x + 59, area.y, area.width - 62, area.height, item?.Name ?? "空置", 17, item != null ? Paper : BagQuiet, item != null);
        if (GUI.Button(area, GUIContent.none, GUIStyle.none)) FocusGearSlot(slot);
    }

    static string BagSlotName(CultivationGearSlot slot) => slot switch
    {
        CultivationGearSlot.Weapon => "武器",
        CultivationGearSlot.Clothing => "防具",
        CultivationGearSlot.Accessory => "法宝",
        _ => "装备"
    };

    void DrawBagCarrySlots(CultivatorBag bag)
    {
        Block(new Rect(94, 539, 558, 1), BagEdge);
        Words(94, 552, 126, 46, "战斗携带", 19, Paper, true);
        for (int slot = 0; slot < CultivatorBag.CarrySlotCount; slot++)
        {
            var item = bag.Carried(slot);
            var area = new Rect(226 + slot * 214, 552, 210, 46);
            bool focused = bagCarryFocus == slot;
            Block(area, focused ? Gold : BagEdge);
            Block(new Rect(area.x + 1, area.y + 1, area.width - 2, area.height - 2),
                focused || area.Contains(Event.current.mousePosition) ? Pine : BagInset);
            Words(area.x + 10, area.y, 55, area.height, CarryKeyName(slot), 17, BagWater, true);
            Words(area.x + 60, area.y, 143, area.height, item?.Name ?? "空置", 17, item != null ? Paper : BagQuiet, item != null);
            if (GUI.Button(area, GUIContent.none, GUIStyle.none)) FocusCarrySlot(slot, bag);
        }
        string hint = "切磋重试复原携带品。野外消耗实物。";
        if (bag.ReservedSlots > 0) hint += $"　为卷首物资预留 {bag.ReservedSlots} 格。";
        Words(95, 602, 554, 20, hint, 13, BagQuiet);
    }

    static string CarryKeyName(int slot) => slot == 0 ? "Z" : "X";

    void FocusCarrySlot(int slot, CultivatorBag bag)
    {
        bagCarryFocus = slot;
        bagFocusSlot = CultivationGearSlot.None;
        bagFocusEquipped = false;
        selectedItem = bag.Carried(slot)?.Id;
        selectedStack = 0;
        confirmDiscard = false;
        bagNotice = null;
    }

    void DrawBagDetail(CultivatorBag bag)
    {
        var item = FirstVolumeItems.Find(selectedItem);
        string section = bagCarryFocus >= 0 ? $"战斗携带 · {CarryKeyName(bagCarryFocus)} 槽" :
            bagFocusEquipped ? "身上装备" : "物件细览";
        Words(718, 224, 282, 28, section, 15, BagQuiet, true);
        Block(new Rect(718, 257, 282, 1), BagEdge);
        if (item != null || selectedItem == FreshGrass) DrawSelectedBagItem(bag, item);
        else if (bagCarryFocus >= 0)
        {
            Words(718, 272, 285, 51, "携带槽空置", 25, Paper, true);
            Words(718, 336, 282, 84, "在行囊中选中回春丹或破阵符，再选要装入的槽位。", 17, BagQuiet);
        }
        else if (bagView == BagView.Gear && bagFocusSlot != CultivationGearSlot.None)
        {
            Words(718, 272, 285, 51, BagSlotName(bagFocusSlot) + "位空着", 25, Paper, true);
            Words(718, 338, 282, 76, "取得同类装备后，可在行囊中选中并穿戴。", 17, BagQuiet);
        }
        else Words(718, 280, 283, 50, "没有选中的物件", 23, Paper, true);

        if (bagNotice != null) Words(718, 567, 282, 25, bagNotice, 14, BagJade);
        DrawBagClaims(bag);
    }

    void DrawBagClaims(CultivatorBag bag)
    {
        if (!State.Free) return;
        bool starting = !bag.StartingGearClaimed, trial = !bag.BattleKitClaimed;
        if (starting && Action(718, 591, trial ? 137 : 282, 29, "领取青溪行装"))
        {
            if (bag.TryClaimStartingGear())
            {
                FocusGearSlot(CultivationGearSlot.Weapon);
                Save();
                bagNotice = "青溪短剑与粗布衣已收入行囊。";
            }
            else bagNotice = "留出两格行囊空间后可领行装。";
        }
        if (trial && Action(starting ? 864 : 718, 591, starting ? 136 : 282, 29, "领取试斗用品", true))
        {
            if (bag.TryClaimBattleKit())
            {
                ResetBagPanel();
                selectedItem = FirstVolumeItems.SpiritPendant;
                Save();
                bagNotice = "已领灵纹玉佩和两枚回春丹。";
            }
            else bagNotice = "行囊空格不足，可整理后再领。";
        }
    }

    void DrawSelectedBagItem(CultivatorBag bag, CultivationItem item)
    {
        string title = item?.Name ?? "鲜青纹草";
        string kind = item?.GearSlot == CultivationGearSlot.Accessory ? "法宝 · 饰品" : item?.KindName ?? "委托物件";
        string description = item?.Description ?? "从活株留根剪取的一份鲜药。交给青溪药铺时按原先的分成结账。";
        DrawBagBadge(new Rect(718, 275, 56, 56), selectedItem);
        Words(790, 269, 209, 38, title, 25, Paper, true);
        Words(790, 307, 210, 24, kind, 15, Gold);
        Words(718, 339, 280, 71, description, 17, Paper);
        Block(new Rect(718, 416, 282, 1), BagEdge);
        if (item != null && item.GearSlot != CultivationGearSlot.None)
        {
            DrawBagGearDetails(bag, item);
            DrawBagGearAction(bag, item);
            return;
        }
        if (item != null && item.CanCarry)
        {
            DrawBagCarriedDetails(bag, item);
            return;
        }
        if (item == null)
        {
            Words(718, 447, 282, 41, "持有 1 件　·　独立收纳", 16, BagQuiet);
            return;
        }
        int count = bag.Count(item.Id);
        Words(718, 447, 282, 41, $"持有 {count} 件　·　每叠至多 {item.StackSize} 件", 16, BagQuiet);
        if (State.Free && (item.Id == FirstVolumeItems.JadeHerb || item.Id == FirstVolumeItems.FoundationPill))
            DrawBagDiscardAction(bag, item);
    }

    void DrawBagGearDetails(CultivatorBag bag, CultivationItem item)
    {
        string modifier = item.AttackBonus > 0 ? $"术威 +{item.AttackBonus}" : "";
        if (item.HealthBonus > 0)
            modifier += (modifier.Length > 0 ? "　" : "") + $"气血上限 +{item.HealthBonus}";
        if (modifier.Length > 0) Words(718, 422, 282, 28, modifier, 19, BagJade, true);
        var spell = item.GrantedSpell;
        if (spell == null)
        {
            Words(718, 459, 282, 29, bagFocusEquipped ? "已穿戴 · 装备位收纳" : $"行囊持有 {bag.Count(item.Id)} 件", 15, BagQuiet);
            return;
        }
        Words(718, 451, 282, 23, $"附术  {spell.Name}  ·  射程 {spell.Range} 格", 17, BagWater, true);
        Words(718, 476, 282, 21, $"伤害 +{spell.ExtraPower}　法力 {spell.ManaCost}　道藏 {spell.CostText}", 14, Paper);
        Words(718, 499, 282, 21, $"冷却 {spell.CooldownTurns} 次本人行动 · 独立法宝技", 14, BagQuiet);
    }

    void DrawBagCarriedDetails(CultivatorBag bag, CultivationItem item)
    {
        string effect = item.CarryEffect == CultivationCarryEffect.Heal ? $"最多回复气血 {item.CarryHeal}" : "松风隘斗法专用";
        Words(718, 424, 282, 29, $"{effect}　·　行动力 {item.CarryActionCost}", 17, BagJade, true);
        Words(718, 458, 282, 47, bagCarryFocus >= 0 ?
            $"已装入 {CarryKeyName(bagCarryFocus)} 槽。轮到本人时，可在斗法中使用。" :
            $"行囊内有 {bag.Count(item.Id)} 件。每槽可装一件，需在斗法前备好。", 15, BagQuiet);
        if (!State.Free) return;
        if (bagCarryFocus >= 0)
        {
            if (!Action(718, 526, 282, 38, "放回行囊")) return;
            if (!bag.TryUnpack(bagCarryFocus))
            {
                bagNotice = "行囊空格不足，携带物仍在原槽。";
                return;
            }
            ResetBagPanel();
            bagView = item.Id == FirstVolumeItems.RoadSeal ? BagView.Commissions : BagView.Remedies;
            selectedItem = item.Id;
            Save();
            bagNotice = $"已将{item.Name}放回行囊。";
            return;
        }
        for (int slot = 0; slot < CultivatorBag.CarrySlotCount; slot++)
            DrawPackAction(bag, item, slot);
    }

    void DrawPackAction(CultivatorBag bag, CultivationItem item, int slot)
    {
        var carried = bag.Carried(slot);
        float x = 718 + 145 * slot;
        string key = CarryKeyName(slot);
        if (carried?.Id == item.Id)
        {
            Words(x + 10, 526, 126, 38, $"{key} 槽已装", 16, BagQuiet);
            return;
        }
        if (!Action(x, 526, 137, 38, carried == null ? $"装入 {key} 槽" : $"更换 {key} 槽", true)) return;
        if (!bag.TryPack(slot, item.Id))
        {
            bagNotice = "行囊空格不足，无法换回原携带物。";
            return;
        }
        if (bag.Count(item.Id) == 0) FocusCarrySlot(slot, bag);
        Save();
        bagNotice = $"已将{item.Name}装入 {key} 槽。";
    }

    void DrawBagGearAction(CultivatorBag bag, CultivationItem item)
    {
        if (!State.Free) return;
        var worn = bag.Equipped(item.GearSlot);
        if (!bagFocusEquipped && worn == item)
        {
            Words(718, 526, 282, 38, "身上已穿戴同款装备", 16, BagQuiet);
            return;
        }
        string action = bagFocusEquipped ? "卸回行囊" : worn != null ? "换上装备" : "穿戴装备";
        if (!Action(718, 526, 282, 38, action, true)) return;
        bool success = bagFocusEquipped ? bag.TryUnequip(item.GearSlot) : bag.TryEquip(item.Id);
        if (!success)
        {
            bagNotice = "行囊没有空格容纳取下的装备。";
            return;
        }
        var view = bagView;
        var scroll = bagScroll;
        FocusGearSlot(item.GearSlot);
        if (view == BagView.Physical)
        {
            bagView = view;
            bagScroll = scroll;
        }
        Save();
        bagNotice = bagFocusEquipped ? $"已装备{item.Name}。" : $"已将{item.Name}放回行囊。";
    }

    void DrawBagDiscardAction(CultivatorBag bag, CultivationItem item)
    {
        if (!confirmDiscard)
        {
            if (Action(718, 526, 282, 38, "弃去一件"))
            {
                confirmDiscard = true;
                bagNotice = null;
            }
            return;
        }
        if (Action(718, 526, 179, 38, "确认弃去一件", true))
        {
            if (bag.TryRemove(item.Id, 1))
            {
                Save();
                bagNotice = $"已弃去一件{item.Name}。";
            }
            confirmDiscard = false;
        }
        else if (Action(906, 526, 94, 38, "留下")) confirmDiscard = false;
    }

    void DrawBagBadge(Rect area, string id)
    {
        var (glyph, color) = id switch
        {
            FirstVolumeItems.TravelBlade => ("剑", BagWater),
            FirstVolumeItems.TravelCoat => ("衣", Paper),
            FirstVolumeItems.SpiritPendant => ("佩", BagWater),
            FirstVolumeItems.SpringPill => ("丹", BagJade),
            FirstVolumeItems.JadeHerb => ("芝", BagJade),
            FirstVolumeItems.FoundationPill => ("丹", BagClay),
            FirstVolumeItems.MeridianDew => ("露", BagWater),
            FreshGrass => ("草", BagJade),
            FirstVolumeItems.RoadSeal => ("符", Gold),
            _ => ("物", Paper)
        };
        Block(area, BagInset);
        Block(new Rect(area.x, area.yMax - 2, area.width, 2), color);
        BagCentered(area, glyph, area.width > 45 ? 29 : 22, color, true);
    }

    void BagCentered(Rect area, string text, int size, Color color, bool bold = false)
    {
        var alignment = label.alignment;
        label.alignment = TextAnchor.MiddleCenter;
        Words(area.x, area.y, area.width, area.height, text, size, color, bold);
        label.alignment = alignment;
    }
}
